

using System;
using System.Collections.Generic;
using VigiothCapital.QuantTrader.Data;
using VigiothCapital.QuantTrader.Data.Consolidators;

namespace VigiothCapital.QuantTrader.ToolBox
{
    /// <summary>
    /// Provides an implementation of <see cref="IDataProcessor"/> that consolidates the data
    /// stream and forwards the consolidated data to other processors
    /// </summary>
    public class ConsolidatorDataProcessor : IDataProcessor
    {
        private DateTime _frontier;
        private readonly IDataProcessor _destination;
        private readonly Func<BaseData, IDataConsolidator> _createConsolidator;
        private readonly Dictionary<Symbol, IDataConsolidator> _consolidators;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsolidatorDataProcessor"/> class
        /// </summary>
        /// <param name="destination">The receiver of the consolidated data</param>
        /// <param name="createConsolidator">Function used to create consolidators</param>
        public ConsolidatorDataProcessor(IDataProcessor destination, Func<BaseData, IDataConsolidator> createConsolidator)
        {
            _destination = destination;
            _createConsolidator = createConsolidator;
            _consolidators = new Dictionary<Symbol, IDataConsolidator>();
        }

        /// <summary>
        /// Invoked for each piece of data from the source file
        /// </summary>
        /// <param name="data">The data to be processed</param>
        public void Process(BaseData data)
        {
            // grab the correct consolidator for this symbol
            IDataConsolidator consolidator;
            if (!_consolidators.TryGetValue(data.Symbol, out consolidator))
            {
                consolidator = _createConsolidator(data);
                consolidator.DataConsolidated += OnDataConsolidated;
                _consolidators[data.Symbol] = consolidator;
            }

            consolidator.Update(data);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _frontier = DateTime.MaxValue;

            // check the other consolidators to see if they also need to emit their working bars
            foreach (var consolidator in _consolidators.Values)
            {
                consolidator.Scan(_frontier);
            }

            _destination.Dispose();
            _consolidators.Clear();
        }

        /// <summary>
        /// Handles the <see cref="IDataConsolidator.DataConsolidated"/> event
        /// </summary>
        private void OnDataConsolidated(object sender, BaseData args)
        {
            _destination.Process(args);

            // we've already checked this frontier time, so don't scan the consolidators
            if (_frontier >= args.EndTime) return;
            _frontier = args.EndTime;

            // check the other consolidators to see if they also need to emit
            foreach (var consolidator in _consolidators.Values)
            {
                // back up the time a single instance, this allows data at exact same
                // time to still come through
                consolidator.Scan(args.EndTime.AddTicks(-1));
            }
        }
    }
}