using System;
using System.Text;

public class ManagedProto : IProto, IServiceProto, IManagedProto {
    IProto proto;
    DateTime lastEvent;
    TimeSpan watchdogPeriod;

    public bool ReadyToDie {get {
        return (DateTime.Now-lastEvent) > watchdogPeriod;
    }}

    public ManagedProto (IProto proto, TimeSpan watchdogPeriod) {
        this.proto = proto;
        this.watchdogPeriod = watchdogPeriod;
        lastEvent = DateTime.Now;
    }

    public bool Holds (IProto p) {
        if (proto is IServiceProto) {
            return ((IServiceProto)proto).Holds (p);
        } else {
            return proto == p;
        }
    }

    public StringBuilder StepCanSend () {
        StringBuilder ret = proto.StepCanSend ();
        if (ret != null) {
            lastEvent = DateTime.Now;
        }
        return ret;
    }

    public void StepCantSend () {
        proto.StepCantSend();
    }

    public void HandleMsg (string msg, int offset) {
        lastEvent = DateTime.Now;
        proto.HandleMsg(msg, offset);
    }
}
