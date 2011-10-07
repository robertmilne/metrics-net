﻿using System;
using metrics.Stats;
using metrics.Support;
using Newtonsoft.Json;

namespace metrics.Core
{
    /// <summary>
    /// A meter metric which measures mean throughput and one-, five-, and fifteen-minute exponentially-weighted moving average throughputs
    /// </summary>
    /// <see href="http://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average">EMA</see>
    public class MeterMetric : IMetric, IMetered 
    {
        private static readonly long _interval = TimeSpan.FromSeconds(5).Ticks;

        public static MeterMetric New(string eventType, TimeUnit rateUnit)
        {
            var meter = new MeterMetric(eventType, rateUnit);
            
            return meter;
        }

        private readonly EWMA _m1Rate = EWMA.OneMinuteEWMA();
        private readonly EWMA _m5Rate = EWMA.FiveMinuteEWMA();
        private readonly EWMA _m15Rate = EWMA.FifteenMinuteEWMA();
        
        private readonly AtomicLong _count = new AtomicLong();
        private readonly long _startTime = DateTime.Now.Ticks;

        private MeterMetric(string eventType, TimeUnit rateUnit)
        {
            EventType = eventType;
            RateUnit = rateUnit;
        }

        /// <summary>
        /// Returns the meter's rate unit
        /// </summary>
        /// <returns></returns>
        public TimeUnit RateUnit { get; private set; }

        /// <summary>
        /// Returns the type of events the meter is measuring
        /// </summary>
        /// <returns></returns>
        public string EventType { get; private set; }

        private void Tick()
        {
            _m1Rate.Tick();
            _m5Rate.Tick();
            _m15Rate.Tick();
        }

        /// <summary>
        /// Mark the occurrence of an event
        /// </summary>
        public void Mark()
        {
            Mark(1);
        }

        /// <summary>
        /// Mark the occurrence of a given number of events
        /// </summary>
        public void Mark(long n)
        {
            _count.AddAndGet(n);
            _m1Rate.Update(n);
            _m5Rate.Update(n);
            _m15Rate.Update(n);
        }

        /// <summary>
        ///  Returns the number of events which have been marked
        /// </summary>
        /// <returns></returns>
        public long Count
        {
            get { return _count.Get(); }
        }

        /// <summary>
        /// Returns the fifteen-minute exponentially-weighted moving average rate at
        /// which events have occured since the meter was created
        /// <remarks>
        /// This rate has the same exponential decay factor as the fifteen-minute load
        /// average in the top Unix command.
        /// </remarks> 
        /// </summary>
        public double FifteenMinuteRate
        {
            get
            {
                return _m15Rate.Rate(RateUnit);
            }
        }

        /// <summary>
        /// Returns the five-minute exponentially-weighted moving average rate at
        /// which events have occured since the meter was created
        /// <remarks>
        /// This rate has the same exponential decay factor as the five-minute load
        /// average in the top Unix command.
        /// </remarks>
        /// </summary>
        public double FiveMinuteRate
        {
            get
            {
                return _m5Rate.Rate(RateUnit);
            }
        }

        /// <summary>
        /// Returns the mean rate at which events have occured since the meter was created
        /// </summary>
        public double MeanRate
        {
            get
            {
                if (Count != 0)
                {
                    var elapsed = (DateTime.Now.Ticks - _startTime) * 100; // 1 DateTime Tick == 100ns
                    return ConvertNanosRate(Count / (double)elapsed);
                }
                return 0.0;
            }
        }

        /// <summary>
        /// Returns the one-minute exponentially-weighted moving average rate at
        /// which events have occured since the meter was created
        /// <remarks>
        /// This rate has the same exponential decay factor as the one-minute load
        /// average in the top Unix command.
        /// </remarks>
        /// </summary>
        /// <returns></returns>
        public double OneMinuteRate
        {
            get
            {
                return _m1Rate.Rate(RateUnit);    
            }
        }
        
        private double ConvertNanosRate(double ratePerNs)
        {
            return ratePerNs * RateUnit.ToNanos(1);
        }

        [JsonIgnore]
        public IMetric Copy
        {
            get { return new MeterMetric(EventType, RateUnit);}
        }
    }
}
