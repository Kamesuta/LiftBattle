using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public struct MoveData : IReplicateData
    {
        public bool Jump;
        public float Horizontal;
        public MoveData(bool jump, float horizontal)
        {
            Jump = jump;
            Horizontal = horizontal;
            _tick = 0;
        }

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
    
    
    [SerializeField]
    private float _jumpForce = 15f;
    [SerializeField]
    private float _moveRate = 15f;

    private PredictionRigidbody2D PredictionRigidbody { get; } = new();
    private bool _jump;

    private void Update()
    {
        if (base.IsOwner)
        {
            if (Input.GetButtonDown("Jump"))
                _jump = true;
        }
    }

    private void Awake()
    {
        PredictionRigidbody.Initialize(GetComponent<Rigidbody2D>());
    }

    public override void OnStartNetwork()
    {
        base.TimeManager.OnTick += TimeManager_OnTick;
        base.TimeManager.OnPostTick += TimeManager_OnPostTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnTick -= TimeManager_OnTick;
        base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }
    private void TimeManager_OnTick()
    {
        //Debug.Log($"Tick: {(IsServerStarted ? "Server-Side" : "Client-Side")}");
        Move(BuildMoveData());
    }

    private void TimeManager_OnPostTick()
    {
        CreateReconcile();
    }

    private MoveData BuildMoveData()
    {
        if (!IsOwner)
            return default;

        float horizontal = Input.GetAxisRaw("Horizontal");
        var md = new MoveData(_jump, horizontal);
        _jump = false;

        return md;
    }

    private MoveData _lastCreatedInput = default;

    [Replicate]
    private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        //If inputs are not known. You could predict
        //all the way into CurrentFuture, which would be
        //real-time with the client. Though the more you predict
        //in the future the more you are likely to mispredict.
        if (state == ReplicateState.ReplayedFuture)
        {
            uint lastCreatedTick = _lastCreatedInput.GetTick();
            //If it's only been 1 tick since the last created
            //input then run the logic below.
            //This essentially means if the last created tick
            //was 100, this logic would run if the future tick was 101
            //but not beyond that.
            uint thisTick = md.GetTick();
            if ((thisTick - lastCreatedTick) <= 1)
            {
                md = _lastCreatedInput;
                //Be sure to set the tick back to what it was even if in the future.
                md.SetTick(thisTick);
            }
        }
        //If created data then set as lastCreatedInput.
        else if (state == ReplicateState.ReplayedCreated)
        {
            //If MoveData contains fields which could generate garbage you
            //probably want to dispose of the lastCreatedInput
            //before replacing it. This step is optional.
            _lastCreatedInput.Dispose();
            //Assign newest value as last.
            _lastCreatedInput = md;
        }

        /* ReplicateState is set based on if the data is new, being replayed, ect.
        * Visit the ReplicationState enum for more information on what each value
        * indicates. At the end of this guide a more advanced use of state will
        * be demonstrated. */
        var forces = new Vector3(md.Horizontal * _moveRate, 0f, 0f);
        PredictionRigidbody.AddForce(forces);

        // Jump
        if (md.Jump)
        {
            Debug.Log($"Jump: {(IsServerStarted ? "Server-Side" : "Client-Side")}, {state}");
            var jumpForce = new Vector3(0f, _jumpForce, 0f);
            PredictionRigidbody.AddForce(jumpForce, ForceMode2D.Impulse);
        }

        //Simulate the added forces.
        PredictionRigidbody.Simulate();
    }

    public override void CreateReconcile()
    {
        /* The base.IsServer check is not required but does save a little
        * performance by not building the reconcileData if not server. */
        if (IsServerStarted)
        {
            var rd = new ReconcileData(PredictionRigidbody);
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
}
