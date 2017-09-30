using System;
using System.Text;
/*!
 *  \author  4o
 *  \brief service IProto. implements "spoilage" detection logic for wrapped IProto
 */
public class ManagedProto : IProto, IServiceProto, IManagedProto {
    /*!
     *  \author  4o
     *  \brief wrapped IProto
     */
    IProto proto;
    /*!
     *  \author  4o
     *  \brief last activity timestamp
     */
    DateTime lastEvent;
    /*!
     *  \author  4o
     *  \brief "spoilage" time
     */
    TimeSpan watchdogPeriod;
    /*!
     *  \author  4o
     *  \brief SessionProtoCollection gets "spoilage" status with this method
     */
    public bool ReadyToDie {get {
        return (DateTime.Now-lastEvent) > watchdogPeriod;
    }}
    /*!
     *  \author  4o
     *  \brief constructor...
     *  \param proto wrapped proto
     *  \param watchdogPeriod "spoilage" time
     */
    public ManagedProto (IProto proto, TimeSpan watchdogPeriod) {
        this.proto = proto;
        this.watchdogPeriod = watchdogPeriod;
        lastEvent = DateTime.Now;
    }
    /*!
     *  \author  4o
     *  \brief equals function for wrapped IProto
     *  \param p IProto to check equality to
     */
    public bool Holds (IProto p) {
        if (proto is IServiceProto) {
            return ((IServiceProto)proto).Holds (p);
        } else {
            return proto == p;
        }
    }
    /*!
     *  \author  4o
     *  \brief Step() function allowing message transmission
     */
    public StringBuilder StepCanSend () {
        StringBuilder ret = proto.StepCanSend ();
        return ret;
    }
    /*!
     *  \author  4o
     *  \brief Step() function forbidding message transmission
     */
    public void StepCantSend () {
        proto.StepCantSend();
    }
    /*!
     *  \author  4o
     *  \brief incoming message is passed with this method
     */
    public void HandleMsg (string msg, int offset) {
        lastEvent = DateTime.Now;
        proto.HandleMsg(msg, offset);
    }
}
