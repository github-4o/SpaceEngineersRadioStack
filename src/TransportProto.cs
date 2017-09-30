using System;
using System.Text;
using System.Collections.Generic;

/*!
 *  \author  4o
 *  \brief service IProto. ensures message delivery and duplicates detection
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
 *           if initiator get a reply with invalid message number (not the one it sent last)
 *           then it resends the message
 *           on receiving side, if message number for `data` message equals
 *           last `data` message number, then 'new' message is discarded
 */
public class TransportProto : IProto, IServiceProto {
    /*!
     *  \author  4o
     *  \brief unique IProto id.
     */
    const string protoId = "transport4o";
    /*!
     *  \author  4o
     *  \brief predefined string used in parser
     */
    const string prefix = protoId + "%";
    /*!
     *  \author  4o
     *  \brief reply request type message is sent after this period
     */
    readonly TimeSpan resendTime = new TimeSpan (0,0,5); // 5 sec
    /*!
     *  \author  4o
     *  \brief predefined string used in parser
     */
    const string resendRequestMsg = protoId + "%r%";
    /*!
     *  \author  4o
     *  \brief predefined string used in parser
     */
    const string ackMsgPrefix = protoId + "%a%";
    /*!
     *  \author  4o
     *  \brief internal fsm states
     */
    enum State {Idle, SendMsg, WaitReply};
    /*!
     *  \author  4o
     *  \brief internal fsm
     */
    State state = State.Idle;
    /*!
     *  \author  4o
     *  \brief wrapped IProto
     */
    IProto proto;
    /*!
     *  \author  4o
     *  \brief passing through IManagedProto method
     */
    public bool ReadyToDie {get{
        if (proto is IManagedProto) {
            return ((IManagedProto)proto).ReadyToDie;
        } else {
            return false;
        }
    }}

    // tx
    /*!
     *  \author  4o
     *  \brief currently transmitted message
     */
    StringBuilder msg;
    /*!
     *  \author  4o
     *  \brief timestamp of last event related to `msg`
     */
    DateTime msgTimestamp = DateTime.Now + new TimeSpan (0,0,-10);
    /*!
     *  \author  4o
     *  \brief valid flag for msgTimestamp
     */
    bool msgTimestampValid = true;
    /*!
     *  \author  4o
     *  \brief message counter for outgoing messages. used for duplicates
     *         detection
     */
    int msgCnt = -1;

    // tx queue
    /*!
     *  \author  4o
     *  \brief messages scheduled for transmission by the wrapped IProto
     */
    List<StringBuilder> toSendMsgs = new List<StringBuilder> ();

    // rx
    /*!
     *  \author  4o
     *  \brief message counter for incoming messages. used for duplicates
     *         detection
     */
    string lastRxCnt = "0";

    /*!
     *  \author  4o
     *  \brief constructor...
     *  \param p proto to wrap
     */
    public TransportProto (IProto p) {
        proto = p;
    }
    /*!
     *  \author  4o
     *  \brief equality check for the wrapped IProto
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
     *  \brief Step(), which allows message sending
     */
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
    /*!
     *  \author  4o
     *  \brief Step(), which forbids message sending. nothing to do here.
     */
    public void StepCantSend () {
        internalStep ();
        proto.StepCantSend();
    }
    /*!
     *  \author  4o
     *  \brief check if incoming message is a valid ping
     */
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
    /*!
     *  \author  4o
     *  \brief returns "request reply" message
     */
    StringBuilder resentReply () {
        return new StringBuilder (ackMsgPrefix + lastRxCnt + "%");
    }
    /*!
     *  \author  4o
     *  \brief fsm shift. wrapped as this is common for both Step() functions
     */
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
