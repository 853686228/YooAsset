using TMPro;
using UnityEngine.Playables;

namespace Samples.Timeline.TestCode.MySimpleDialogueTrack
{
    public class DialogueBehaviour: PlayableBehaviour
    {
        public string dialogueLine = "";

        // public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        // {
        //     TextMeshProUGUI textComponent = playerData as TextMeshProUGUI;
        //
        //     //如果文本组件为空，则直接返回
        //     if (textComponent == null)
        //         return;
        //     
        //     textComponent.text = dialogueLine;
        // }
    }
}