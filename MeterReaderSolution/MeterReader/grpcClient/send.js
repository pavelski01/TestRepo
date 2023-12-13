import { Timestamp } from "google-protobuf/google/protobuf/timestamp_pb.js";
import { MeterReadingServiceClient } from "./meterservice_grpc_web_pb.js";
import { ReadingPacket, ReadingStatus, ReadingMessage } from "./meterservice_pb.js";

const theLog = document.getElementById("theLog");
const theButton = document.getElementById("theButton");

function addToLog(msg) {
    const div = document.createElement("div");
    div.innerText = msg;
    theLog.appendChild(div);
}

theButton.addEventListener("click", function () {
    try {
        addToLog("Starting Service Call");

        const packet = new ReadingPacket();
        packet.setSuccessful(ReadingStatus.SUCCESS);

        const reading = new ReadingMessage();
        reading.setCustomerid(1);
        reading.setReadingvalue(1000);

        const time = new Timestamp();
        const now = Date.now();
        time.setSeconds(Math.round(now / 1000));

        reading.setReadingtime(time);

        packet.addReadings(reading);

        addToLog("Calling Service");
        const client = new MeterReadingServiceClient(window.location.origin);

        client.addReading(packet, {}, function (err, response) {
            if (err) {
                addToLog(`Error: ${err}`);
            } else {
                addToLog(`Success: ${response.getNotes()}`);
            }
        });

    } catch {
        addToLog("Exception thrown");
    }
});