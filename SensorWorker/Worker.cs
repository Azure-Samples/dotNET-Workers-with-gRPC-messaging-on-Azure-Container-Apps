using Sensors;
using System.Diagnostics;

namespace SensorWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly SensorTwin.SensorTwinClient _sensorTwinClient;
        private Random _random = new Random();
        private double _phaseStartTemp;
        private double _phaseEndTemp;
        private double _currentValue;
        private double _phaseDurationSeconds;
        private const double MinVal = 50;
        private const double MaxVal = 85;
        private const int MinPhaseDuration = 5;
        private const int MaxPhaseDuration = 30;
        private Random _rnd = new Random();
        private Stopwatch _totalTime = Stopwatch.StartNew();

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _sensorTwinClient = new Sensors.SensorTwin.SensorTwinClient(Grpc.Net.Client.GrpcChannel.ForAddress(configuration.GetValue<string>("SERVICE_ENDPOINT")));

            StartNewPhase();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var randomSensorName = new Random().Next(1000, 9999);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Sending sensor reading: {_currentValue}");

                UpdateTemperatureReading();
                var result = await _sensorTwinClient.ReceiveValueFromTwinAsync(new ReceiveValueFromTwinRequest
                {
                    Sensor = $"{Environment.MachineName}-{randomSensorName}",
                    Value = Math.Round(_currentValue, 2)
                });

                _logger.LogInformation($"Sensor worker received: {result.Message}.");

                await Task.Delay(100, stoppingToken);
            }
        }

        public void UpdateTemperatureReading()
        {
            double startRads;
            double endRads;
            double offset;
            if (_phaseStartTemp >= _phaseEndTemp)
            {
                // Cooling phase, that means moving from PI/2 to 3*PI/2
                startRads = Math.PI / 2;
                endRads = 3 * Math.PI / 2;
                offset = -1;
            }
            else
            {
                // Heading phase, that means moving from 3*PI/2 to 5*PI/2
                startRads = 3 * Math.PI / 2;
                endRads = 5 * Math.PI / 2;
                offset = 1;
            }

            var currentSeconds = _totalTime.Elapsed.TotalSeconds;
            var currentRads = startRads + (currentSeconds * Math.PI / _phaseDurationSeconds);

            _currentValue = _phaseStartTemp + ((offset + Math.Sin(currentRads)) / 2) * Math.Abs(_phaseStartTemp - _phaseEndTemp);

            if (currentRads >= endRads)
            {
                StartNewPhase();
            }
        }

        private void StartNewPhase()
        {
            _phaseStartTemp = _currentValue;
            _phaseEndTemp = MinVal + _rnd.NextDouble() * (MaxVal - MinVal);
            _phaseDurationSeconds = _rnd.Next(MinPhaseDuration, MaxPhaseDuration);
            _totalTime.Restart();
        }
    }
}