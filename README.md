# RTP over UDP implementation for A/V multicast-streaming

## Objetive
* Analyze the impact on the network by the A/V streaming. Calculate delay, jitter and losses.
* Implementation of RTP Protocol. [Spec of RTP](https://tools.ietf.org/html/rfc3550)
* Chat-multicast over UDP
* Webcam USB multicast-streaming (jpeg compression) 
* Microphone multicast-streaming (encoded with a-law[g711])

> Disclaimer: The sound only reproduce if the videochat-client form is focus. 

## Wireshark RTP decoding
If we capture the traffic of videochat-server with wireshark, we can decode the UDP packets with the RTP spec. In case of JPEG frames, we find a RTP Stream and in other case, the audio streaming, we find too a diferent RTP Stream

[Usage of wireshark for RTP statistics](https://wiki.wireshark.org/RTP_statistics)

Pics of a example traffic capture!
![6](https://user-images.githubusercontent.com/30501761/71902876-3ccfdc00-3163-11ea-9e62-a3b7f8be719b.PNG)
![7](https://user-images.githubusercontent.com/30501761/71902878-3d687280-3163-11ea-9712-163a178f03b9.PNG)
![8](https://user-images.githubusercontent.com/30501761/71902879-3d687280-3163-11ea-9a07-5705291a4bbb.PNG)

## Analyze
On the client, we implement the delay, jitter and losses for every RTP received. We calculate this values for each 1sg

<!---
![Capture](https://user-images.githubusercontent.com/30501761/71902871-3c374580-3163-11ea-8451-16f5c15f2f5e.PNG)
![3](https://user-images.githubusercontent.com/30501761/71902873-3ccfdc00-3163-11ea-9893-53411c17e6cb.PNG)
![4](https://user-images.githubusercontent.com/30501761/71902874-3ccfdc00-3163-11ea-998b-5a1b05c885fd.PNG)
---> 

Pics of client!
![5](https://user-images.githubusercontent.com/30501761/71902875-3ccfdc00-3163-11ea-8e6c-03e95c1e2ae6.PNG)


Pics of server!
![2](https://user-images.githubusercontent.com/30501761/71902872-3ccfdc00-3163-11ea-9319-b55b4bba0003.PNG)


[Spec of RTP. Appendix for jitter calc](https://tools.ietf.org/html/rfc3550#appendix-A.8)

[Analysis of RTP Packet Delay and Loss](https://www.ece.rutgers.edu/~marsic/books/CN/projects/wireshark/ws-project-4.html)

### File explanation
* ALaw. Teacher files to encode and decode audio with ALaw
* RTPStream. My implementation of RTP spec. Create the RTP header, create the packet and send it over the network.
* videochat-client. Client that connect to the net, receiving the incoming RTP A/V packets and show that to the user (reproduce and display).
* videochat-server. Server to wrap the audio and the video, encapsulate and send over the net.

### External references
* WebCamLib + WebCamWrapper => C++ Lib that teacher give to wrap USB cameras
* DirectX DirectSound => To capture the sound
* RTPStream => My implementation of RTP Spec
* ALaw => To encode/decode PCM.

