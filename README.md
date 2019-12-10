# benzuber-client

C# client for Benzuber.

Sample project: [BenzuberClientTestApp](https://github.com/hevk112/benzuber-client/blob/master/BenzuberClientTestApp)

[Client usage sample](https://github.com/hevk112/benzuber-client/blob/master/BenzuberClientTestApp/Program.cs)

<B>1. Initialize client:</B>

1.1 Create configuration:
```
//Fuel station ID form benzuber server administrator  
var stationId = 0;
//Hardware ID - for binding station id to PC. HEX string len: 32, generate by control system
var hwid = "1415D518C428500F70F688CE04D8EA47";
//Server address
var server = "https://test-as-apiazs.benzuber.ru";
//Creating configuration
var config = new Configuration(stationId, hwid, httpsTestAsApiazsBenzuberRu, Logger.LogLevels.Debug)
```
1.2 Create client:
```
client = new Client(config);
```
1.3 Set callbacks for set and cancel orders:
```
client.OrderToSetReceivedFunc = OnOrderToSetReceived;
client.OrderToCancelReceivedFunc = OnOrderToCancelReceived;
```
1.4 Start polling:
```
//CreatePricesInfo - return current price and pump configuration for station by control system
var pricesInfo = CreatePricesInfo();
//CreatePumpStates - return current pump states by control system
var stationInformation = new StationInformation(CreatePumpStates());
//Token Source for cancellation polling
var cts = new CancellationTokenSource();
var polling = client.StartPolling(stationInformation, pricesInfo, cts.Token);
```
<B>2. Set prices:</B>
```
//Set current price info
client.SetPrice(CreatePricesInfo());
```
<B>3. Pump state:</B>
```
// Setted fuelCode indicate that the nozzle up on pump. Else set fuelCode = 0
client.SetPumpState(new PumpState(pumpNumber, isAvailable, fuelCode));
```
