using System;
using System.Text;
using System.Collections.Generic;

/*!
 *  \author  4o
 *  \brief ensures message delivery and duplicates detection
 *  \details ensures message delivery and duplicates detection
 *           the message of this proto should start with a type
 *           a message of this proto could be of 3 types:
 *           1. data, marked with %m%
 *           2. reply (ack), marked with %a%
 *           3. reply request, marked with %r%
 *           `data` and `reply` message types should also contain message number field
 *           message number in `reply` type indicate message number filed in the last accepted
 *           `data` message
 *           if initiator receives no reply, it should request it
 *           if initiator get a reply with invalid message numner (not the one it sent last)
 *           then it resends the message
 *           on receiving side, if message number for `data` message equals
 *           last `data` message number, then 'new' message is discarded
 *  \coryright BSD 3-clause
 */
public class TransportProto : IProto, IServiceProto {
    const string protoId = "transport4o";

    const string prefix = protoId + "%";
    readonly TimeSpan resendTime = new TimeSpan (0,0,5); // 5 sec
    const string resendRequestMsg = protoId + "%r%";
    const string ackMsgPrefix = protoId + "%a%";

    enum State {Idle, SendMsg, WaitReply};
    State state = State.Idle;

    IProto proto;
    public bool ReadyToDie {get{
        if (proto is IManagedProto) {
            return ((IManagedProto)proto).ReadyToDie;
        } else {
            return false;
        }
    }}

    // tx

    StringBuilder msg;
    DateTime msgTimestamp = DateTime.Now + new TimeSpan (0,0,-10);
    bool msgTimestampValid = true;

    int msgCnt = -1;

    // tx queue

    List<StringBuilder> toSendMsgs = new List<StringBuilder> ();

    // rx

    string lastRxCnt = "0";

    public TransportProto (IProto p) {
        proto = p;
    }

    public bool Holds (IProto p) {
        if (proto is IServiceProto) {
            return ((IServiceProto)proto).Holds (p);
        } else {
            return proto == p;
        }
    }

    public StringBuilder StepCanSend () {
        if (toSendMsgs.Count > 0) {
            StringBuilder ret = toSendMsgs [0];
            toSendMsgs.RemoveAt (0);
            return ret;
        }
        switch (state) {
            case State.SendMsg:
                msgTimestampValid = true;
                msgTimestamp = DateTime.Now;
                state = State.WaitReply;
                return msg;
            case State.WaitReply:
                if (msgTimestampValid) {
                    if ((DateTime.Now - msgTimestamp) > resendTime) {
                        msgTimestamp = DateTime.Now;
                        return new StringBuilder (resendRequestMsg);
                    }
                }
                break;
        }
        internalStep ();
        return null;
    }

    public void StepCantSend () {
        internalStep ();
        proto.StepCantSend();
    }

    public void HandleMsg (string msg, int offset) {
        if (msg.IndexOf (prefix, offset) == offset) {
            offset = offset + prefix.Length;
            char msgType = msg[offset];
            int cntEndOffset;
            string newRxCnt;
            switch (msgType) {
                // init
                case 'm': // message
                    cntEndOffset = msg.IndexOf ('%', offset+2);
                    if (cntEndOffset > 0) {
                        newRxCnt = msg.Substring (offset+2, cntEndOffset - offset-2);
                        if (lastRxCnt != newRxCnt) {
                            lastRxCnt = newRxCnt;
                            toSendMsgs.Add (resentReply());
                            proto.HandleMsg (msg, cntEndOffset+1);
                        }
                    }
                    break;
                case 'r': // resend reply
                    toSendMsgs.Add (resentReply());
                    break;
                // respond
                case 'a': // ack
                    switch (state) {
                        case State.WaitReply:
                            cntEndOffset = msg.IndexOf ('%', offset+2);
                            if (cntEndOffset > 0) {
                                newRxCnt = msg.Substring (offset+2, cntEndOffset - offset-2);
                                if (newRxCnt == (msgCnt-1).ToString()) {
                                    msgTimestampValid = false;
                                    state = State.Idle;
                                }
                            }
                            break;
                    }
                    break;
            }
        }
    }

    StringBuilder resentReply () {
        return new StringBuilder (ackMsgPrefix + lastRxCnt + "%");
    }

    void internalStep () {
        if (state == State.Idle) {
            msg = proto.StepCanSend ();
            if (msg != null) {
                msg.Insert (0, protoId + "%m%" + msgCnt + "%");
                if (msgCnt > 255) { // cause this thing is ported from irl proto
                    msgCnt = 0;
                } else {
                    msgCnt ++;
                }
                state = State.SendMsg;
            }
        }
    }
}
