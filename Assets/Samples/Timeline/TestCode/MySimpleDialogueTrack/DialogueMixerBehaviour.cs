using TMPro;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Samples.Timeline.TestCode.MySimpleDialogueTrack
{
    public class DialogueMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            TextMeshProUGUI textComponent = playerData as TextMeshProUGUI;

            //如果文本组件为空，则直接返回
            if (textComponent == null)
                return;

            string finalContent = "";
            float totalWeight = 0f;

            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);

                //如果输入权重大于0，说明正在播放
                if (inputWeight > 0)
                {
                    ScriptPlayable<DialogueBehaviour> inputPlayable =
                        (ScriptPlayable<DialogueBehaviour>) playable.GetInput(i);
                    var inputBehaviour = inputPlayable.GetBehaviour();
                    finalContent = inputBehaviour.dialogueLine;
                    totalWeight += inputWeight;
                }
            }

            // 3. 应用到 UI
            // 这里的逻辑意味着：如果没有片段在播放，text 就会变成 "" (清空字幕)
            textComponent.text = finalContent;
        }
    }
}