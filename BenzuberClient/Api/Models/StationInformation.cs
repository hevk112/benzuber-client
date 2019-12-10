using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Benzuber.Api.Models
{
    public class StationInformation
    {
        public int PumpsCount { get; }
        private readonly ConcurrentDictionary<int, PumpState> _pumpStates = new ConcurrentDictionary<int, PumpState>();

        public StationInformation(ICollection<PumpState> pumpStates)
        {
            foreach (var pumpState in pumpStates)
            {
                _pumpStates[pumpState.PumpNo] = pumpState;
            }
            PumpsCount = pumpStates.Count;
        }

        public void SetPumpState(PumpState state)
        {
            if (!_pumpStates.ContainsKey(state.PumpNo))
                throw new ArgumentOutOfRangeException(nameof(state), "Pump not found in initial data");
            _pumpStates[state.PumpNo] = state;
        }

        public override string ToString()
        {
            return string.Join(";", _pumpStates.Select(pump => $"{pump.Key}:{pump.Value.State}"));
        }
    }
}
