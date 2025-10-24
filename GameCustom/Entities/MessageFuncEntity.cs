using System;

using Logic.GameCustom.Abstracts;

namespace Logic.GameCustom.Entities
{
    public class MessageFuncEntity<T> : IMessage
    {
        public readonly Func<T, T> Function;

        public MessageFuncEntity(Func<T, T> function)
        {
            Function = function;
        }

        public T Execute(T parameter) => Function.Invoke(parameter);
    }    
    public class MessageActionEntity<T> : IMessage
    {
        public readonly Func<T> Function;

        public MessageActionEntity(Func<T> function)
        {
            Function = function;
        }

        public T Execute() => Function.Invoke();
    }
    public class MessageActionEntity : IMessage
    {
        public readonly Action Function;

        public MessageActionEntity(Action function)
        {
            Function = function;
        }

        public void Execute() => Function.Invoke();
    }
}