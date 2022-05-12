using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Sensors;

namespace SensorService.Services
{
    public class SensorTwinService : Sensors.SensorTwin.SensorTwinBase
    {
        const string CACHE_KEY = "READINGS_";
        public IMemoryCache MemoryCache { get; set; }

        public SensorTwinService(IMemoryCache memoryCache)
        {
            MemoryCache = memoryCache;
        }

        private void SetRecentReadings(Dictionary<string, List<double>> input) => MemoryCache.Set<Dictionary<string, List<double>>>(CACHE_KEY, input);

        private Dictionary<string, List<double>> GetRecentReadings()
        {
            var tmp = new Dictionary<string, List<double>>();
            if (!MemoryCache.TryGetValue<Dictionary<string, List<double>>>(CACHE_KEY, out tmp))
            {
                tmp = new Dictionary<string, List<double>>();
                SetRecentReadings(tmp);
            }

            return tmp;
        }

        public override async Task GetDeviceTwinStream(Empty request, IServerStreamWriter<ReceivedValueFromTwinReply> responseStream, ServerCallContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var readings = GetRecentReadings();
                SetRecentReadings(new Dictionary<string, List<double>>());

                foreach (var item in readings)
                {
                    foreach (var value in item.Value)
                    {
                        await responseStream.WriteAsync(new ReceivedValueFromTwinReply
                        {
                            Message = $"Received {value} from sensor {item.Key}.",
                            Value = value,
                            Sensor = item.Key
                        });
                    }
                }

                await Task.Delay(100);
            }
        }

        public override Task<ReceivedValueFromTwinReply> ReceiveValueFromTwin(ReceiveValueFromTwinRequest request, ServerCallContext context)
        {
            var readings = GetRecentReadings();

            lock(readings)
            {
                if (!readings.Any(x => x.Key == request.Sensor))
                    readings.Add(request.Sensor, new List<double>());

                if (readings[request.Sensor].Count >= 100)
                    readings[request.Sensor].RemoveAt(0);

                readings[request.Sensor].Add(request.Value);
                SetRecentReadings(readings);

                return Task.FromResult(new ReceivedValueFromTwinReply
                {
                    Message = $"Received {request.Value} from {request.Sensor}.",
                    Sensor = request.Sensor,
                    Value = request.Value
                });
            }
            
        }
    }
}
