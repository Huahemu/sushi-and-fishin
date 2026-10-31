using System;
using System.Collections;
using PaperPixelGames.Core.Ai.NPCs;
using TMPro;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Source control tags use their own entry on the new friend adapter. The
    // original ResidentBillboardText and its ordinary typewriter stay intact.
    public sealed class FriendSourceBillboard : MonoBehaviour
    {
        ResidentBillboardText billboard;
        TMP_Text text;
        Coroutine routine;
        Action complete;
        public int SourceCharacterCount => text ? text.textInfo.characterCount : 0;
        public bool IsTyping => routine != null;

        public static FriendSourceBillboard GetOrAdd(ResidentBillboardText target)
        {
            if (!target || !target.GetComponentInParent<FriendWorldActor>()) return null;
            var adapter = target.GetComponent<FriendSourceBillboard>();
            if (!adapter) adapter = target.gameObject.AddComponent<FriendSourceBillboard>();
            adapter.billboard = target;
            adapter.text = target.GetComponentInChildren<TMP_Text>(true);
            return adapter;
        }
        public void PlaySourceTypewriter(string message, float speed, float secondsPerCharacter, int alreadyVisible, Action onComplete = null)
        {
            Cancel();
            if (!billboard || !text) { onComplete?.Invoke(); return; }
            // Public baseline calls handle its visibility, layout and smart
            // alignment. CompleteInstant ends only the just-created baseline
            // coroutine; this adapter then owns source timing.
            billboard.PlayTypewriter(message ?? "");
            billboard.CompleteInstant();
            text.ForceMeshUpdate();
            int count = text.textInfo.characterCount;
            text.maxVisibleCharacters = Mathf.Clamp(alreadyVisible, 0, count);
            complete = onComplete;
            float delay = secondsPerCharacter >= 0 ? secondsPerCharacter : .01f / Mathf.Max(.01f, speed);
            routine = StartCoroutine(Type(count, text.maxVisibleCharacters, delay));
        }
        IEnumerator Type(int count, int start, float delay)
        {
            // One initial yield ensures routine has been assigned before even
            // an empty or instant run calls its completion callback.
            yield return null;
            for (int i = start; i < count && text; i++)
            {
                text.maxVisibleCharacters = i + 1;
                if (delay > 0) yield return new WaitForSeconds(delay);
            }
            if (text) text.maxVisibleCharacters = int.MaxValue;
            routine = null; var callback = complete; complete = null; callback?.Invoke();
        }
        public void CompleteInstant()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null; if (text) text.maxVisibleCharacters = int.MaxValue;
            var callback = complete; complete = null; callback?.Invoke();
        }
        public void Hide() { Cancel(); if (billboard) billboard.Hide(); }
        void Cancel() { if (routine != null) StopCoroutine(routine); routine = null; complete = null; }
        void OnDisable() { Cancel(); }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class FriendResidentExpressionProjection : MonoBehaviour
    {
        ResidentController actor;
        float expression, until;
        static readonly int hash = Animator.StringToHash("Expression");
        public static bool TryPlay(ResidentController target, string sourceName, float duration)
        {
            if (!target || !target.GetComponent<FriendWorldActor>() || !target.VisualAnimator) return false;
            float value;
            switch (sourceName)
            {
                case "Neutral": case "Normal": value = (float)ResidentExpressionType.Neutral; break;
                case "Smile": value = (float)ResidentExpressionType.Smile; break;
                case "Happy": value = (float)ResidentExpressionType.Happy; break;
                case "Angry": value = (float)ResidentExpressionType.Angry; break;
                case "Sad": case "Sorrow": value = (float)ResidentExpressionType.Sad; break;
                case "Surprise": value = (float)ResidentExpressionType.Surprised; break;
                default: return false;
            }
            bool represented = false;
            foreach (var parameter in target.VisualAnimator.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Float) { represented = true; break; }
            if (!represented) return false;
            var projection = target.GetComponent<FriendResidentExpressionProjection>();
            if (!projection) projection = target.gameObject.AddComponent<FriendResidentExpressionProjection>();
            projection.actor = target; projection.expression = value; projection.until = Time.time + Mathf.Max(.1f, duration);
            return true;
        }
        void LateUpdate()
        {
            if (actor && actor.VisualAnimator && Time.time < until) actor.VisualAnimator.SetFloat(hash, expression);
        }
    }
}
