import grpc

from google.protobuf.timestamp_pb2 import Timestamp
from meterservice_pb2 import ReadingPacket, ReadingStatus, ReadingMessage
from meterservice_pb2_grpc import MeterReadingServiceStub

def main():
    print("Starting Client Call")

    packet = ReadingPacket(Successful = ReadingStatus.Success)

    now = Timestamp()
    now.GetCurrentTime()

    reading = ReadingMessage(CustomerId = 1, ReadingTime = now, ReadingValue = 10000)
    packet.Readings.append(reading)

    channel = grpc.insecure_channel("localhost:8888")
    stub = MeterReadingServiceStub(channel)

    response = stub.AddReading(packet)
    if (response.Status == ReadingStatus.Success):
        print("Succeeded")
    else:
        print("Failed")

main()