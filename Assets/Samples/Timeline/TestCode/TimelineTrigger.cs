using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Samples.Timeline.TestCode
{
    public class TimelineTrigger : MonoBehaviour
    {
        private PlayableDirector _director;

        private void Start()
        {
            _director = GetComponent<PlayableDirector>();
            ResetPlayerAnimBinding();
        }

        private void ResetPlayerAnimBinding()
        {
            var timeline = _director.playableAsset as TimelineAsset;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track.name == "MyCharacterTrack")
                {
                    var playerB = GameObject.Find("Staff_vs_Staff_OneFight_MaleB");
                    var animator = playerB.GetComponent<Animator>();
                    
                    //_director.GetGenericBinding()
                    _director.SetGenericBinding(track, animator);
                    break;
                }
            }
            
            _director.Play();
        }
    }
}