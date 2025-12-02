using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Samples.Timeline.TestCode.MySimpleDialogueTrack
{
    [Serializable] //必须添加，否则无法被Timeline识别
    public class DialogueClip: PlayableAsset, ITimelineClipAsset
    {
        //用于Editor下输入对话文本、保存文本，Runtime下会被DialogueBehaviour使用
        [TextArea(3,10)]
        public string content;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogueBehaviour>.Create(graph);
            
            var behaviour = playable.GetBehaviour();
            //把编辑器中的数据传给Runtime实例
            behaviour.dialogueLine = content;
            return playable;
        }

        //文本不支持混合
        public ClipCaps clipCaps => ClipCaps.None;
    }
}