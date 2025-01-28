using System.Collections.Generic;
using UnityEngine;

public class PlayerAudioComponent : PlayerBaseComponent
{
    [SerializeField] private AudioSource source;

    [Header("Walking Sounds")]
    [SerializeField] private List<AudioClip> walkingSounds;
    [SerializeField] private float interval;

    private IMovingPawn moveableContext;

    private float currentInterval;

    public override void Initialize(P_PlayerPawn context)
    {
        base.Initialize(context);

        moveableContext = context as IMovingPawn;
    }

    public override void UpdateTick(out bool swallowTick)
    {
        swallowTick = false;

        if (!initialized) return;
        if (moveableContext == null) return;

        if (moveableContext.IsMoving)
        {
            currentInterval += Time.deltaTime;

            if (currentInterval >= interval)
            {
                currentInterval = 0;

                //randomize pitch
                source.pitch = Random.Range(0.8f, 1.2f);
                source.PlayOneShot(walkingSounds[Random.Range(0, walkingSounds.Count)]);
            }
        }
    }
}
