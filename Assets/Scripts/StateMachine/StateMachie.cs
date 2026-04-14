using System;
using System.Collections.Generic;

namespace Platformer
{
    public class StateMachie
    {
        StateNode current;
        Dictionary<Type, StateNode> nodes = new();

        class StateNode
        {
            public IState State { get; }

            public HashSet<ITransition> Transitions { get; }

            public StateNode(IState state)
            {
                State = state;
                Transitions = new HashSet<ITransition>();
            }

            public void AddTransition(IState to, IPredicate condition)
            {
                Transition.Add(item:new Transition(to, condition));
            }
        }
    }

}
