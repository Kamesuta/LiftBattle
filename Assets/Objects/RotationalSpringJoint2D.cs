using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationalSpringJoint2D : NetworkBehaviour
{
    public float springStrength = 1;
    public float damperStrength = 1;

    private PredictionRigidbody2D PredictionRigidbody { get; } = new();

    private void Awake()
    {
        PredictionRigidbody.Initialize(GetComponent<Rigidbody2D>());
    }

    // Update is called once per frame
    public override void OnStartNetwork()
    {
        base.TimeManager.OnPostTick += TimeManager_OnPostTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }

    [Replicate]
    private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        /* ReplicateState is set based on if the data is new, being replayed, ect.
        * Visit the ReplicationState enum for more information on what each value
        * indicates. At the end of this guide a more advanced use of state will
        * be demonstrated. */
        var springTorque = springStrength * Vector3.Cross(PredictionRigidbody.Rigidbody2D.transform.up, Vector3.up);
        var dampTorque = damperStrength * -PredictionRigidbody.Rigidbody2D.angularVelocity;

        PredictionRigidbody.AddTorque(springTorque.z + dampTorque, ForceMode2D.Force);

        //Simulate the added forces.
        PredictionRigidbody.Simulate();
    }

    private void TimeManager_OnPostTick()
    {
        Move(default);
        /* The base.IsServer check is not required but does save a little
        * performance by not building the reconcileData if not server. */
        if (IsServerStarted)
        {
            ReconcileData rd = new ReconcileData(PredictionRigidbody);
            Reconciliation(rd);
        }
    }

    public override void CreateReconcile()
    {
        /* The base.IsServer check is not required but does save a little
        * performance by not building the reconcileData if not server. */
        if (IsServerStarted)
        {
            ReconcileData rd = new ReconcileData(PredictionRigidbody);
            //if (!base.IsOwner)
            //    Debug.LogError($"Frame {Time.frameCount}. Reconcile, MdTick {LastMdTick}, PosX {transform.position.x.ToString("0.##")}. VelX {Rigidbody.velocity.x.ToString("0.###")}.");
            Reconciliation(rd);
        }
    }

    [Reconcile]
    private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
    {
        //Sets state of transform and rigidbody.
        Rigidbody2D rb = PredictionRigidbody.Rigidbody2D;
        rb.SetState(rd.RigidbodyState);
        //Applies reconcile information from predictionrigidbody.
        PredictionRigidbody.Reconcile(rd.PredictionRigidbody);
    }

    public struct MoveData : IReplicateData
    {
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct ReconcileData : IReconcileData
    {
        //As of 4.1.3 you can use RigidbodyState to send
        //the transform and rigidbody information easily.
        public Rigidbody2DState RigidbodyState;
        //As of 4.1.3 PredictionRigidbody was introduced.
        //It primarily exists to create reliable simulations
        //when interacting with triggers and collider callbacks.
        public PredictionRigidbody2D PredictionRigidbody;

        public ReconcileData(PredictionRigidbody2D pr)
        {
            RigidbodyState = new Rigidbody2DState(pr.Rigidbody2D);
            PredictionRigidbody = pr;
            _tick = 0;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}
