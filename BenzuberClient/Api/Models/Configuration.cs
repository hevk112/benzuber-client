using ProjectSummer.Repository;

namespace Benzuber.Api.Models
{
    public  class Configuration
    {
        public int StationId { get; }
        public string Hwid { get; }
        public string Server { get; }

        public Logger.LogLevels LogLevel { get; }

        public Configuration(int stationId, string hwid, string server, Logger.LogLevels logLevel)
        {
            this.StationId = stationId;
            this.Hwid = hwid;
            this.Server = server;
            this.LogLevel = logLevel;
        }
    }
}
