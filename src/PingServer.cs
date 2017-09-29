using System;
using System.Text;

public class PingServer : IProto {
    const string protoId = "pingProtoServer";

    bool sendPong = false;

    public StringBuilder StepCanSend () {
        if (sendPong) {
            sendPong = false;
            return new StringBuilder ("pingProtoServer%pong");
        }
        return null;
    }
    public void StepCantSend () {

    }

    public void HandleMsg (string msg, int offset) {
        if (msg == null) {
            throw new Exception ("null msg");
        }
        if (sendPong) {
            return;
        }
        if (msg.Substring (offset) == "pingProtoClient%ping") {
            sendPong = true;
        } else {
            throw new Exception ("invalid msg: " + msg.Substring (offset) + " offset = " + offset);
        }
    }
}
