using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using TMPro;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Friend billboards outside the E menu: short pair-chat lines (Say) and
    // the problem mark (Tomodachi problem bubble). Uses only each friend's
    // own original ResidentBillboardText; never writes over another line.
    public sealed class FriendInteractionPresenter : MonoBehaviour
    {
        public const string ProblemMark = "！有烦恼";
        sealed class Line { public string person, text; public float start, until; public bool shown; }
        static FriendInteractionPresenter instance;
        readonly List<Line> lines = new List<Line>();
        readonly Dictionary<string, ResidentBillboardText> billboards = new Dictionary<string, ResidentBillboardText>();
        float nextMarks;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install() { _ = Instance; }
        static FriendInteractionPresenter Instance
        {
            get
            {
                if (!instance) { var go = new GameObject("Friend interaction bubbles"); DontDestroyOnLoad(go); instance = go.AddComponent<FriendInteractionPresenter>(); }
                return instance;
            }
        }
        public static void Say(string personId, string text, float delay, float seconds)
        {
            if (string.IsNullOrEmpty(personId) || string.IsNullOrEmpty(text)) return;
            var self = Instance; self.lines.RemoveAll(l => l.person == personId && !l.shown);
            self.lines.Add(new Line { person = personId, text = text, start = Time.unscaledTime + delay, until = Time.unscaledTime + delay + seconds });
        }
        ResidentBillboardText Billboard(FriendsLifeRuntime runtime, string personId)
        {
            if (billboards.TryGetValue(personId, out var cached) && cached) return cached.isActiveAndEnabled ? cached : null;
            var actor = runtime.ActorOf(personId); var found = actor ? actor.GetComponentInChildren<ResidentBillboardText>(true) : null;
            if (found) billboards[personId] = found; else billboards.Remove(personId);
            return found && found.isActiveAndEnabled ? found : null;
        }
        // The open E menu (or NPC offer) owns that friend's billboard.
        static bool InDialogue(string personId)
        {
            var panel = Singleton<GameUIManager>.TryGetInstance(out var ui) ? ui.DialoguePanelController : null;
            return panel && panel.IsVisible && (FriendWorldTalkController.ActivePersonId == personId || FriendSocialDialogueController.ActivePersonId == personId);
        }
        static string Shown(ResidentBillboardText billboard)
        {
            var text = billboard.GetComponentInChildren<TMP_Text>(true);
            return text && text.gameObject.activeInHierarchy ? text.text ?? "" : "";
        }
        void Update()
        {
            var runtime = FriendsLifeRuntime.Instance; if (!runtime || !runtime.Ready) return;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i]; if (Time.unscaledTime < line.start) continue;
                var billboard = Billboard(runtime, line.person);
                if (!line.shown)
                {
                    if (billboard && !InDialogue(line.person)) billboard.PlayTypewriter(line.text);
                    line.shown = true; continue;
                }
                if (Time.unscaledTime < line.until) continue;
                if (billboard && !InDialogue(line.person) && Shown(billboard) == line.text) billboard.Hide();
                lines.RemoveAt(i); nextMarks = 0;
            }
            if (Time.unscaledTime < nextMarks) return; nextMarks = Time.unscaledTime + .5f;
            foreach (var person in runtime.People.Where(p => p.controllerKind == PersonControllerKind.Npc))
            {
                if (lines.Any(l => l.person == person.personId) || InDialogue(person.personId)) continue;
                var billboard = Billboard(runtime, person.personId); if (!billboard) continue;
                string shown = Shown(billboard); bool problem = FriendPlayerInteractions.Problem(runtime, person.personId) != null;
                if (problem && shown.Length == 0) billboard.PlayTypewriter(ProblemMark);
                else if (!problem && shown == ProblemMark) billboard.Hide();
            }
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
