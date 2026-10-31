using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SushiAndFish.FriendsLife
{
    // Managed HTTP respects the existing desktop proxy configuration. Unity's
    // native upload did not use the configured Windows proxy in the actual QA
    // run; no global proxy setting, certificate policy or credentials are changed.
    public static class PhotoFriendHttpProvider
    {
        public static async Task<byte[]> Generate(PhotoFriendDraft draft,Action save,CancellationToken cancellation)
        {
            using(var handler=new HttpClientHandler())
            {
                var proxy=Proxy();if(proxy!=null){handler.UseProxy=true;handler.Proxy=proxy;}
                using(var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(180)})
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("SushiAndFish-FriendsLife/1.0");
                    string host=(!string.IsNullOrEmpty(draft.jobProvider)?draft.jobProvider:Environment.GetEnvironmentVariable("SUSHI_PHOTO_3D_ENDPOINT")??draft.provider).TrimEnd('/');
                    if(string.IsNullOrEmpty(draft.remoteResultUrl))
                    {
                        if(string.IsNullOrEmpty(draft.uploadedPath))
                        {
                            using(var content=new MultipartFormDataContent())
                            {var file=new ByteArrayContent(File.ReadAllBytes(draft.cutoutPath));file.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");content.Add(file,"files","cutout.png");using(var response=await client.PostAsync(host+"/upload",content,cancellation)){Ensure(response);draft.uploadedPath=(string)JArray.Parse(await response.Content.ReadAsStringAsync())[0];save();}}
                        }
                        var input=new JObject{["path"]=draft.uploadedPath,["orig_name"]="cutout.png",["meta"]=new JObject{["_type"]="gradio.FileData"}};
                        bool hunyuan=host.IndexOf("hunyuan",StringComparison.OrdinalIgnoreCase)>=0;
                        if(!hunyuan&&!string.IsNullOrEmpty(draft.eventId)&&(draft.remotePhase==4||draft.remotePhase==6))
                        {draft.remoteButton=await Wait(client,host,draft,false,save,cancellation);draft.eventId=null;save();}
                        if(string.IsNullOrEmpty(draft.eventId))
                        {
                            if(hunyuan){draft.remotePhase=5;await Submit(client,host,draft,5,new JArray(null,input,null,null,null,null,20,5.0,1234,128,false,8000,false),save,cancellation);}
                            else
                            {
                                if(string.IsNullOrEmpty(draft.remoteButton))
                                {draft.remotePhase=4;await Submit(client,host,draft,4,new JArray(input,.85),save,cancellation);draft.remoteButton=await Wait(client,host,draft,false,save,cancellation);draft.eventId=null;save();}
                                if(draft.remoteButton=="Remove Background")
                                {draft.remotePhase=6;await Submit(client,host,draft,5,new JArray("Remove Background",input,null,.85,"None",-1,1024),save,cancellation);draft.remoteButton=await Wait(client,host,draft,false,save,cancellation);draft.eventId=null;save();}
                                draft.remotePhase=5;await Submit(client,host,draft,5,new JArray("Run",input,null,.85,"None",-1,1024),save,cancellation);
                            }
                        }
                        await Wait(client,host,draft,true,save,cancellation);
                    }
                    using(var response=await client.GetAsync(draft.remoteResultUrl,cancellation))
                    {if(response.StatusCode==HttpStatusCode.NotFound){draft.remoteResultUrl=null;draft.eventId=null;save();}Ensure(response);return await response.Content.ReadAsByteArrayAsync();}
                }
            }
        }
        static async Task Submit(HttpClient client,string host,PhotoFriendDraft draft,int fn,JArray data,Action save,CancellationToken cancellation)
        {
            var body=new JObject{["data"]=data,["fn_index"]=fn,["session_hash"]=draft.sessionId,["event_data"]=null,["trigger_id"]=null};
            using(var content=new StringContent(body.ToString(Formatting.None),Encoding.UTF8,"application/json"))using(var response=await client.PostAsync(host+"/queue/join",content,cancellation))
            {Ensure(response);draft.eventId=(string)JObject.Parse(await response.Content.ReadAsStringAsync())["event_id"];if(string.IsNullOrEmpty(draft.eventId))throw new InvalidOperationException("生成服务未返回任务编号");save();}
        }
        static async Task<string> Wait(HttpClient client,string host,PhotoFriendDraft draft,bool model,Action save,CancellationToken cancellation)
        {
            using(var request=new HttpRequestMessage(HttpMethod.Get,host+"/queue/data?session_hash="+Uri.EscapeDataString(draft.sessionId)))
            using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation))
            {
                Ensure(response);using(var stream=await response.Content.ReadAsStreamAsync())using(cancellation.Register(()=>stream.Dispose()))using(var reader=new StreamReader(stream))
                {
                    while(true)
                    {
                        cancellation.ThrowIfCancellationRequested();string line=await reader.ReadLineAsync();if(line==null)throw new IOException("生成服务连接中断，草稿已保留");if(!line.StartsWith("data: ",StringComparison.Ordinal))continue;
                        var message=JObject.Parse(line.Substring(6));if((string)message["event_id"]!=draft.eventId||(string)message["msg"]!="process_completed")continue;
                        if((bool?)message["success"]!=true){draft.eventId=null;save();throw new InvalidOperationException("生成服务失败："+((string)message["output"]?["error"]??"上游未提供错误详情"));}
                        if(!model)return (string)message["output"]?["data"]?[0]?["value"]??"Run";var values=message["output"]?["data"]as JContainer;JObject file=null;if(values!=null)foreach(var item in values.DescendantsAndSelf())if(item is JObject candidate&&Path(candidate).EndsWith(".glb",StringComparison.OrdinalIgnoreCase))file=candidate;
                        if(file==null)throw new InvalidOperationException("生成服务没有返回3D模型");draft.remoteResultUrl=(string)file["url"]??host+"/file="+Uri.EscapeDataString(Path(file));save();return null;
                    }
                }
            }
        }
        static string Path(JObject item)=>item["path"]?.Type==JTokenType.String?(string)item["path"]:item["value"]?.Type==JTokenType.String?(string)item["value"]:"";
        static void Ensure(HttpResponseMessage response){if(!response.IsSuccessStatusCode)throw new InvalidOperationException(response.StatusCode==(HttpStatusCode)429?"生成服务繁忙，请稍后重试":"生成服务连接失败（"+(int)response.StatusCode+"）");}
        static IWebProxy Proxy()
        {
            string address=Environment.GetEnvironmentVariable("SUSHI_PHOTO_HTTP_PROXY")??Environment.GetEnvironmentVariable("HTTPS_PROXY")??Environment.GetEnvironmentVariable("HTTP_PROXY");
            if(string.IsNullOrEmpty(address))try{using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings")){if(Convert.ToInt32(key?.GetValue("ProxyEnable",0))==1){address=key.GetValue("ProxyServer","")as string;if(address!=null&&address.Contains("=")){string selected=null;foreach(string part in address.Split(';'))if(part.StartsWith("https=",StringComparison.OrdinalIgnoreCase))selected=part.Substring(6);address=selected;}}}}catch(PlatformNotSupportedException){}catch(UnauthorizedAccessException){}
            if(string.IsNullOrWhiteSpace(address))return WebRequest.GetSystemWebProxy();if(!address.Contains("://"))address="http://"+address;var uri=new Uri(address);var proxy=new WebProxy(uri);if(!string.IsNullOrEmpty(uri.UserInfo)){var parts=uri.UserInfo.Split(new[]{':'},2);proxy.Credentials=new NetworkCredential(Uri.UnescapeDataString(parts[0]),parts.Length>1?Uri.UnescapeDataString(parts[1]):"");}return proxy;
        }
    }
}
