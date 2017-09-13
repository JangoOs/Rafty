using System;
using System.Collections.Generic;
using System.Linq;
using Rafty.Concensus.States;
using Rafty.FiniteStateMachine;
using Rafty.Infrastructure;
using Rafty.Log;

namespace Rafty.Concensus
{
    public class Node : INode
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly ILog _log;
        private readonly Func<CurrentState, List<IPeer>> _getPeers;
        private readonly IRandomDelay _random;
        private readonly Settings _settings;
        private IRules _rules;
        private IPeersProvider _peersProvider;

        public Node(
            IFiniteStateMachine fsm, 
            ILog log, 
            IRandomDelay random, 
            Settings settings,
            IPeersProvider peersProvider)
        {
            //dont think rules should be injected at the moment..EEK UNCLE BOB
            _rules = new Rules();
            _fsm = fsm;
            _log = log;
            _random = random;
            _settings = settings;
            _peersProvider = peersProvider;
            _getPeers = state => {
                var peers = _peersProvider.Get();
                var peersThatAreNotThisServer = peers.Where(p => p?.Id != state.Id).ToList();
                return peersThatAreNotThisServer;
            };
        }

        public IState State { get; private set; }

        public void Start()
        {
            if(State?.CurrentState == null)
            {
                BecomeFollower(new CurrentState(Guid.NewGuid(), 0, default(Guid), 0, 0, default(Guid)));
            }
            else
            {
                BecomeFollower(State.CurrentState);
            }
        }

        public void BecomeCandidate(CurrentState state)
        {
            State.Stop();
            var candidate = new Candidate(state, _fsm, _getPeers(state), _log, _random, this, _settings, _rules);
            State = candidate;
            candidate.BeginElection();
        }

        public void BecomeLeader(CurrentState state)
        {
            State.Stop();
            State = new Leader(state, _fsm, _getPeers(state), _log, this, _settings, _rules);
        }

        public void BecomeFollower(CurrentState state)
        {
            State?.Stop();
            State = new Follower(state, _fsm, _log, _random, this, _settings, _rules, _getPeers(state));
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            return State.Handle(appendEntries);
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            return State.Handle(requestVote);
        }

        public Response<T> Accept<T>(T command)
        {
            return State.Accept(command);
        }
        public void Stop()
        {
            State.Stop();
        }
    }
}