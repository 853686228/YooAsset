using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Samples.Timeline.TestCode
{
    [AddComponentMenu("Samples/Timeline/TestCode/MixerGraphTest")]
    [RequireComponent(typeof(Animator))]
    public class MixerGraphTest : MonoBehaviour
    {
        [Header("Clips")] public AnimationClip ClipA;
        public AnimationClip ClipB;

        [Header("AvatarMask")]
        public AvatarMask AvatarMask;

        [Header("ControlParams")] public float fadeSpeed = 2f;

        private PlayableGraph _playableGraph;
        private AnimationLayerMixerPlayable _layerMixer;
        
        private AnimationClipPlayable _clipPlayable;

        private void Start()
        {
            _playableGraph = PlayableGraph.Create("TestGraph");

            //创建AnimationLayerMixerPlayable , 此处inputCount 2 表示有两个layer层输入混合
            _layerMixer = AnimationLayerMixerPlayable.Create(_playableGraph, 2);
            _layerMixer.SetLayerMaskFromAvatarMask(1, AvatarMask);
            
            _layerMixer.SetInputWeight(0,1);
            _layerMixer.SetInputWeight(1,0); //暂时不让ClipB生效
            
            //创建IPlayableOutput输出节点
            var animOutput = AnimationPlayableOutput.Create(_playableGraph, "AnimOutput", GetComponent<Animator>());

            //IPlayableOutput 通过SetSourcePlayable连接到Playable
            animOutput.SetSourcePlayable(_layerMixer);

            //在Graph中创建playable节点
            var playableA = AnimationClipPlayable.Create(_playableGraph, ClipA);
             _clipPlayable = AnimationClipPlayable.Create(_playableGraph, ClipB);
             var test = AnimationClipPlayable.Create(_playableGraph, ClipB);

            //Graph中 Playable间通过Graph.Connect进行连接 
            // 指定source playable输出端口和dst playable输入端口
            _playableGraph.Connect(playableA, 0, _layerMixer, 0);
            _playableGraph.Connect(_clipPlayable, 0, _layerMixer, 1);

            _playableGraph.Play();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PlayerClipB();
            }
            
            float curWeight = _layerMixer.GetInputWeight(1);
            //说明正在播放
            if (curWeight > 0.01f)
            {
                if (_clipPlayable.GetTime() > ClipB.length)
                {
                    //开始淡出
                    float newWeight = Mathf.Lerp(curWeight, 0, Time.deltaTime * fadeSpeed);
                    _layerMixer.SetInputWeight(1, newWeight);
                }
            }
        }
        private void PlayerClipB()
        {
            _clipPlayable.SetTime(0);//倒带到最开始的位置
            _layerMixer.SetInputWeight(1,1); //打开权重,Override上半身
            _clipPlayable.Play(); //开始播放
        }
        private void OnDestroy()
        {
            _playableGraph.Destroy();
        }
    }
}