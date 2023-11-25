using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


namespace mtion.room.sdk.action
{
    public static class ActionConstants 
    {
        public const string ACTION_ENTRY_POINT = "ActionEntryPoint";
        public const string UPDATE_ENTRY_POINT = "UpdateSynchronizationPoint";
        public const string BIND_TO_ACTION_EXIT = "BindToActionComplete";
        public const string UNBIND_TO_ACTION_EXIT = "UnbindToActionComplete";
    }
    

    public enum ActionInvocationSource
    {
        INTERNAL,
        TWITCH,
        FACEBOOK,
        YOUTUBE,
        OTHER
    }

    [Serializable]
    public sealed class ActionPresence
    {
        public string UserId;
        public string Username;
        public string UserAvatarUrl;
    }

    [Serializable]
    public sealed class ActionPresences
    {
        [FormerlySerializedAs("Reciever")]
        public ActionPresence Receiver;
        public List<ActionPresence> Invokers;
    }

    [Serializable]
    public class CameraInfo
    {
        public CameraInfo() { }
        public CameraInfo(Camera cam)
        {
            if (cam != null)
            {
                Position = cam.transform.position;
                Rotation = cam.transform.rotation;
            }
        }

        public Vector3 Position;
        public Quaternion Rotation;
    }

    [Serializable]
    public sealed class ActionMetadata
    {
        public DateTime TimeStamp;
        public ActionInvocationSource InvocationSource;
        public ActionPresences Presences;
        public CameraInfo CameraInfo;
    }


    public interface IMActionExitEvent
    {
        void BindToActionComplete(Action onActionComplete);
        void UnbindToActionComplete(Action onActionComplete);
    }
    
    public interface IMActionExitParameterProvider
    {
        int Count { get; }
        IReadOnlyList<string> GetParameterNames();
        string GetParameterName(int index);
        Type GetParameterType(string parameterName);
        Type GetParameterType(int index);
        T GetParameterValue<T>(string parameterName);
        T GetParameterValue<T>(int index);
    }

    public interface IAction
    {
        
    }

    public interface IMActionInterfaceImpl : IAction
    {
        public void ActionEntryPoint(ActionMetadata metadata);
    }

    public interface IMActionInterfaceImpl<T0> : IAction
    {
        public void ActionEntryPoint(T0 param0, ActionMetadata metadata);
    }

    public interface IMActionInterfaceImpl<T0, T1> : IAction
    {
        public void ActionEntryPoint(T0 param0, T1 param1, ActionMetadata metadata);
    }

    public interface IMActionInterfaceImpl<T0, T1, T2> : IAction
    {
        public void ActionEntryPoint(T0 param0, T1 param1, T2 param2, ActionMetadata metadata);
    }

    public interface IMActionInterfaceImpl<T0, T1, T2, T3> : IAction
    {
        public void ActionEntryPoint(T0 param0, T1 param1, T2 param2, T3 param3, ActionMetadata metadata);
    }

    public interface IMActionInterfaceImpl<T0, T1, T2, T3, T4> : IAction
    {
        public void ActionEntryPoint(T0 param0, T1 param1, T2 param2, T3 param3, T4 param4, ActionMetadata metadata);
    }

    public interface IMActionInterfaceImpl<T0, T1, T2, T3, T4, T5> : IAction
    {
        public void ActionEntryPoint(T0 param0, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, ActionMetadata metadata);
    }
}
