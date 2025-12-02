using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace Samples.Timeline.TestCode.MySimpleDialogueTrack
{
    [TrackColor(0.1f, 0.5f, 0.8f)] //设置颜色
    [TrackClipType(typeof(DialogueClip))] //绑定Clip类型
    [TrackBindingType(typeof(TextMeshProUGUI))] //绑定绑定组件类型为Text
    [DisplayName("对话轨道")]
    public class DialogueTrack : TrackAsset
    {
        //创建轨道的混合器 (Mixer)
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<DialogueMixerBehaviour>.Create(graph, inputCount);
        }
    }
}