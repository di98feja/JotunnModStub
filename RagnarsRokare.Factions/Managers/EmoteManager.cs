using UnityEngine;

namespace RagnarsRokare.Factions
{
    public class EmoteManager
    {
		public enum Emotes
        {
			Challenge,
			Cheer,
			Nonono,
			Tumbsup,
			Point,
			Wave,
			Sit,
			Drink
        };

        protected static int m_animatorTagEmote = Animator.StringToHash("emote");

		public static int StartEmote(ZNetView nview, Emotes emote, bool oneshot = true)
		{
			int emoteId = nview.GetZDO().GetInt("emoteID") + 1;
			nview.GetZDO().Set("emoteID", emoteId);
			nview.GetZDO().Set("emote", emote.ToString().ToLower());
			nview.GetZDO().Set("emote_oneshot", oneshot);
			return emoteId;
		}

		public static void StopEmote(ZNetView nview)
		{
			if (nview.GetZDO().GetString("emote") != "")
			{
				int emoteId = nview.GetZDO().GetInt("emoteID") + 1;
				nview.GetZDO().Set("emoteID", emoteId);
				nview.GetZDO().Set("emote", "");
			}
		}

		public static void UpdateEmote(ZNetView nview,ref string emoteState,ref int emoteID, Animator animator)
		{
			if (nview.IsOwner() && InEmote(emoteState, animator) && nview.gameObject.GetComponent<Character>().m_moveDir != Vector3.zero)
			{
				StopEmote(nview);
			}
			int currentEmoteId = nview.GetZDO().GetInt("emoteID");
			if (currentEmoteId == emoteID)
			{
				return;
			}
			emoteID = currentEmoteId;
			if (!string.IsNullOrEmpty(emoteState))
			{
				animator.SetBool("emote_" + emoteState, value: false);
			}
			emoteState = "";
			animator.SetTrigger("emote_stop");
			string emoteName = nview.GetZDO().GetString("emote");
			if (!string.IsNullOrEmpty(emoteName))
			{
				bool runOnce = nview.GetZDO().GetBool("emote_oneshot");
				animator.ResetTrigger("emote_stop");
				if (runOnce)
				{
					animator.SetTrigger("emote_" + emoteName);
					return;
				}
				emoteState = emoteName;
				animator.SetBool("emote_" + emoteName, value: true);
			}
		}

		public static bool InEmote(string emoteState, Animator animator)
		{
			if (!string.IsNullOrEmpty(emoteState))
			{
				return true;
			}
			return animator.GetCurrentAnimatorStateInfo(0).tagHash == m_animatorTagEmote;
		}

	}
}
