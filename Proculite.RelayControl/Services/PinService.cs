using System.Device.Gpio;
using Proculite.RelayControl.Models;

namespace Proculite.RelayControl.Services
{
    public class PinService
    {
        private readonly ILogger<PinService> _logger;
        private readonly IConfiguration _configuration;
        private readonly KeyPinControl[] _keyPinControl;
        private readonly Dictionary<string, Pin[]> _pinControlMap;
        private readonly int[] _allPinNumbers;
        private readonly GpioController _gpioController;

        public PinService(IConfiguration configuration, ILogger<PinService> logger)
        {
            _logger = logger;
            _configuration = configuration;
            _keyPinControl = configuration
                .GetSection("KeyPinControl")
                .GetChildren()
                .Select(keyPinControl => new KeyPinControl
                {
                    Key = keyPinControl.GetSection("Key").Get<string>() ?? "",
                    Pins = keyPinControl
                        .GetSection("Pins")
                        .GetChildren()
                        .Select(pin => new Pin
                        {
                            Name = pin.GetSection("Name").Get<string>() ?? "",
                            Number = pin.GetSection("Number").Get<int>(),
                            Active = pin.GetSection("Active").Get<string>() ?? "low"
                        })
                        .ToArray()
                })
                .ToArray();
            _pinControlMap = _keyPinControl.ToDictionary(
                pinControl => pinControl.Key,
                pinControl => pinControl.Pins
            );

            _allPinNumbers = _keyPinControl
                .SelectMany(pinController => pinController.Pins.Select(pin => pin.Number))
                .ToArray();

            _gpioController = new GpioController();
            foreach (int pin in _allPinNumbers)
            {
                _gpioController.OpenPin(pin, PinMode.Output, PinValue.High);
            }
        }

        public Pin[] AccessiblePins(HttpRequest httpRequest)
        {
            string? keyCookie = httpRequest.Cookies["key"];
            bool keyExists = keyCookie is not null && _pinControlMap.ContainsKey(keyCookie);
            Pin[] controlledPins = keyExists ? _pinControlMap[keyCookie] : Array.Empty<Pin>();
            return controlledPins;
        }

        public bool PinIsAccessible(HttpRequest httpRequest, int pinToCheck)
        {
            Pin[] accessiblePins = AccessiblePins(httpRequest);
            return accessiblePins.Any(pin => pin.Number == pinToCheck);
        }

        public Pin PinByNumber(HttpRequest httpRequest, int pinNumber)
        {
            return AccessiblePins(httpRequest).First(pin => pin.Number == pinNumber);
        }

        private void SetPinActive(Pin pin)
        {
            _gpioController.Write(pin.Number, pin.ActiveHigh ? PinValue.High : PinValue.Low);
        }

        private void SetPinInactive(Pin pin)
        {
            _gpioController.Write(pin.Number, pin.ActiveHigh ? PinValue.Low : PinValue.High);
        }

        public void PinOn(HttpRequest request, int pinNumber)
        {
            if (!PinIsAccessible(request, pinNumber))
            {
                return;
            }

            var requestedPin = PinByNumber(request, pinNumber);
            SetPinActive(requestedPin);
        }

        public void PinOff(HttpRequest request, int pinNumber)
        {
            if (!PinIsAccessible(request, pinNumber))
            {
                return;
            }

            var requestedPin = PinByNumber(request, pinNumber);
            SetPinInactive(requestedPin);
        }

        public async Task PinBlink(HttpRequest request, int pinNumber)
        {
            if (!PinIsAccessible(request, pinNumber))
            {
                return;
            }

            var requestedPin = PinByNumber(request, pinNumber);
            SetPinInactive(requestedPin);
            await Task.Delay(200);
            SetPinActive(requestedPin);
            await Task.Delay(1000);
            SetPinInactive(requestedPin);
        }
    }
}
