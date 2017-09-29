using System;
using System.Text;

public class PingClient : IProto {
    const string protoId = "pingProtoClient";

    bool pingSent = false;
    int pingNum = 0;
    int pingCap;
    public bool Done {get {return pingNum > pingCap;}}

    public PingClient (int pingCap) {
        this.pingCap = pingCap;
    }

    public StringBuilder StepCanSend () {
        if (!pingSent) {
            pingSent = true;
            return new StringBuilder ("pingProtoClient%ping");
        }
        return null;
    }

    public void StepCantSend () {

    }

    public void HandleMsg (string msg, int offset) {
        if (pingSent) {
            if (msg == null) {
                throw new Exception ("garbage");
            }
            if (msg.Substring (offset) == "pingProtoServer%pong") {
                pingNum++;
                pingSent = false;
            } else {
                throw new Exception ("got msg: " + msg.Substring (offset) + " offset = " + offset);
            }
        } else {
            if (msg != null) {
                throw new Exception ("garbage");
            }
        }
    }
}
