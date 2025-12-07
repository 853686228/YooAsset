using UnityEngine;
using UnityEngine.Playables;

namespace Samples.Timeline.TestCode.LightMixerTrack
{
    public class LightControlMixerBahaviour : PlayableBehaviour
    {
        private Color _defaultColor;
        private float _defaultIntensity;
        private bool _firstFrame = true;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var trackBinding = playerData as Light;
            if (trackBinding == null)
            {
                return;
            }

            //如果是第一帧，记录下Light的默认值
            if (_firstFrame)
            {
                _defaultColor = trackBinding.color;
                _defaultIntensity = trackBinding.intensity;
                _firstFrame = false;
            }
            
            Color finalColor = Color.black;
            float finalIntensity = 0f;
            float totalWeight = 0f;
            
            //混合输入
            for (int i = 0; i < playable.GetInputCount(); ++i)
            {
                var weight = playable.GetInputWeight(i);
                if (weight > 0)
                {
                    ScriptPlayable<LightControlBehaviour>  inputPlayable = (ScriptPlayable<LightControlBehaviour>)playable.GetInput(i);
                    var inputBehaviour = inputPlayable.GetBehaviour();
                    
                    //简单的线性混合
                    finalColor += inputBehaviour.color * weight;
                    finalIntensity += inputBehaviour.intensity * weight;
                    totalWeight += weight;
                }
            }
            
            //处理剩余权重
            float remainingWeight = 1 - totalWeight;
            if (remainingWeight > 0)
            {
                finalColor  +=  remainingWeight * _defaultColor;
                finalIntensity +=  remainingWeight * _defaultIntensity;
            } 
            
            //应用混合结果
            trackBinding.color = finalColor;
            trackBinding.intensity = finalIntensity;
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);
        }

        public override void OnGraphStop(Playable playable)
        {
            // 注意：这里拿不到 playerData (绑定对象)，所以没法在这里复原
            _firstFrame = true;
        }
    }
}