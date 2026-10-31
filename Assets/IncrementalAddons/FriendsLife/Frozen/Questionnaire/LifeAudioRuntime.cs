using System;
using System.Reflection;
using System.Security;
using System.Diagnostics;
using System.Text;
using System.IO;
using UnityEngine;

namespace TomodachiLife
{
    public sealed class LifeAudioRuntime:MonoBehaviour
    {
        public bool musicEnabled=true, speechEnabled=true;
        public string LastSpeechText{get;private set;}
        public string LastSpeechBackend{get;private set;}
        private object voice;
        private AudioSource music, effects;
        private AudioClip tune;
        private bool speechFailed;
        private Process speechWorker;
        public int LastSpeechWorkerExitCode{get;private set;}=int.MinValue;
        public string LastSpeechWorkerError{get;private set;}="";
        public bool SpeechWorkerRunning=>speechWorker!=null&&!speechWorker.HasExited;
        private void Awake()
        {
            music=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.035f;
            effects=gameObject.AddComponent<AudioSource>();effects.volume=.12f;
            const int sampleRate=22050;int length=sampleRate*16;var samples=new float[length];
            float[] notes={261.626f,329.628f,391.995f,329.628f,293.665f,349.228f,440f,349.228f,329.628f,391.995f,493.883f,391.995f,293.665f,349.228f,391.995f,293.665f};
            for(int i=0;i<length;i++){float time=i/(float)sampleRate;float beat=time%1;float envelope=Mathf.Sin(Mathf.PI*beat)*Mathf.Exp(-beat*2);float f=notes[(int)time%notes.Length];samples[i]=envelope*(Mathf.Sin(2*Mathf.PI*f*time)+.2f*Mathf.Sin(4*Mathf.PI*f*time));}
            tune=AudioClip.Create("Island afternoon",length,1,sampleRate,false);tune.SetData(samples,0);music.clip=tune;music.Play();
        }
        private void Update()
        {
            if(music!=null)music.mute=!musicEnabled;
            if(speechWorker!=null&&speechWorker.HasExited)
            {
                LastSpeechWorkerExitCode=speechWorker.ExitCode;LastSpeechWorkerError=speechWorker.StandardError.ReadToEnd();
                if(LastSpeechWorkerExitCode==0)LastSpeechBackend="Windows SAPI Mandarin local speech worker completed";
                else LastSpeechBackend="Windows speech worker failed: "+LastSpeechWorkerExitCode;
                speechWorker.Dispose();speechWorker=null;
            }
        }
        public bool Speak(string text,ResidentAppearance appearance)
        {
            LastSpeechText=text??"";if(!speechEnabled||string.IsNullOrWhiteSpace(text))return false;
            if(Application.platform==RuntimePlatform.WindowsEditor||Application.platform==RuntimePlatform.WindowsPlayer)
            {
                try
                {
                    StopSpeechWorker();int rate=Mathf.Clamp(((appearance?.voiceSpeed??50)-50)/10,-5,5),pitch=Mathf.Clamp(((appearance?.voicePitch??50)-50)/4,-10,10);
                    int volume=Mathf.Clamp(65+(appearance?.voiceTone??50)/3,40,100);
                    string xml="<pitch absmiddle='"+pitch+"'>"+SecurityElement.Escape(text)+"</pitch>";
                    string payload=Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
                    string script="$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';$speaker=New-Object -ComObject SAPI.SpVoice;foreach($token in $speaker.GetVoices()){if($token.GetDescription() -match 'Huihui|Chinese'){$speaker.Voice=$token;break}};$speaker.Rate="+rate+";$speaker.Volume="+volume+";$words=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+payload+"'));[void]$speaker.Speak($words,8);[void][Runtime.InteropServices.Marshal]::ReleaseComObject($speaker);";
                    string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe");
                    speechWorker=Process.Start(new ProcessStartInfo{FileName=executable,Arguments="-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true});
                    if(speechWorker==null)throw new InvalidOperationException("Speech worker did not start.");
                    LastSpeechWorkerExitCode=int.MinValue;LastSpeechWorkerError="";LastSpeechBackend="Windows SAPI Mandarin hidden local speech worker";return true;
                }
                catch(Exception error){LastSpeechWorkerError=error.Message;StopSpeechWorker();}
            }
            if(!speechFailed)
            {
                try
                {
                    if(voice==null)
                    {
                        var type=Type.GetTypeFromProgID("SAPI.SpVoice");if(type==null)throw new InvalidOperationException("Windows speech voice is unavailable.");
                        voice=Activator.CreateInstance(type);var tokens=Invoke(voice,"GetVoices",null);int count=Convert.ToInt32(Get(tokens,"Count"));
                        for(int i=0;i<count;i++){var token=Invoke(tokens,"Item",new object[]{i});string label=Convert.ToString(Invoke(token,"GetDescription",null));if(label.IndexOf("Huihui",StringComparison.OrdinalIgnoreCase)>=0||label.Contains("Chinese")){Set(voice,"Voice",token);break;}}
                    }
                    int rate=Mathf.Clamp(((appearance?.voiceSpeed??50)-50)/10,-5,5);Set(voice,"Rate",rate);Set(voice,"Volume",90);
                    int pitch=Mathf.Clamp(((appearance?.voicePitch??50)-50)/4,-10,10);string xml="<pitch absmiddle='"+pitch+"'>"+SecurityElement.Escape(text)+"</pitch>";
                    Invoke(voice,"Speak",new object[]{xml,11});LastSpeechBackend="Windows SAPI Mandarin, editable rate/pitch";return true;
                }
                catch(Exception error){speechFailed=true;LastSpeechBackend="Procedural resident voice (SAPI unavailable: "+error.GetType().Name+")";}
            }
            int sampleRate=22050;float seconds=Mathf.Clamp(text.Length*.09f,.2f,4);int length=(int)(sampleRate*seconds);var samples=new float[length];float frequency=180+(appearance?.voicePitch??50)*5;
            for(int i=0;i<length;i++){float time=i/(float)sampleRate;float phase=(time*11)%1;float envelope=Mathf.Sin(phase*Mathf.PI);samples[i]=.2f*envelope*Mathf.Sin(2*Mathf.PI*frequency*time)*(1-time/seconds);}
            var clip=AudioClip.Create("ResidentVoice",length,1,sampleRate,false);clip.SetData(samples,0);effects.PlayOneShot(clip);Destroy(clip,seconds+1);return true;
        }
        private static object Invoke(object target,string member,object[] args)=>target.GetType().InvokeMember(member,BindingFlags.InvokeMethod,null,target,args);
        private static object Get(object target,string member)=>target.GetType().InvokeMember(member,BindingFlags.GetProperty,null,target,null);
        private static void Set(object target,string member,object value)=>target.GetType().InvokeMember(member,BindingFlags.SetProperty,null,target,new[]{value});
        private void StopSpeechWorker(){if(speechWorker==null)return;try{if(!speechWorker.HasExited)speechWorker.Kill();speechWorker.Dispose();}catch{}speechWorker=null;}
        private void OnDestroy(){StopSpeechWorker();if(tune!=null)Destroy(tune);if(voice!=null){try{Invoke(voice,"Speak",new object[]{"",3});System.Runtime.InteropServices.Marshal.ReleaseComObject(voice);}catch{}}}
    }
}
