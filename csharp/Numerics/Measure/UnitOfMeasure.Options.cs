/*---------------------------------------------------------------------------------
  (C) Copyright 2023, Johnson Controls Tyco IP Holdings LLP. 
      Use of this Software is subject to the BSD-2-Clause License. 
---------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace JohnsonControls.Numerics.Measure
{
    public partial class UnitOfMeasure
    {
        /// <summary>
        /// Allowable options to configure the unit of measure system
        /// </summary>
        public sealed class Options
        {
            /// <summary>
            /// Build in options (see units.json)
            /// </summary>
            public static Options Default
            {
                get
                {
                    var assembly = typeof(Options).Assembly;
                    using var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Measure.units.json");
                    return JsonSerializer.Deserialize<Options>(stream!, new JsonSerializerOptions()
                    {
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    })!;
                }
            }

            /// <summary>
            /// [prefix]unit will automatically extend the unit with all the given prefixes with appropriate factors applied (e.g. si would have "k" for kilo, "m" for milli, etc. thus [si]m would result in m, mm, and km). 
            /// </summary>
            public IDictionary<string, IDictionary<string, double>> Prefixes { get; set; } = new Dictionary<string, IDictionary<string, double>>();

            /// <summary>
            /// The units to which all other units derive (lookup si base units for example)
            /// </summary>
            public ISet<string> BaseUnits { get; set; } = new HashSet<string>();

            /// <summary>
            /// Well known units and their expressions (e.g. "[si]Hz, [si+]hertz": "s^-1" defines Hz as 1/s), these units can contain alternative spellings as necessary
            /// </summary>
            public IDictionary<string, string> Units { get; set; } = new Dictionary<string, string>();

            /// <summary>
            /// Controls the amount of time units remain cached resetting with each access
            /// </summary>
            public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(5);

            /// <summary>
            /// The memory pressure threshold at which the cache will be cleared by <see cref="HighMemoryPressureClearPercentage"/>
            /// </summary>
            public int HighMemoryPressureThreshold { get; set; } = 90;

            /// <summary>
            /// The percentage of the cache to clear when the <see cref="HighMemoryPressureThreshold"/> is reached
            /// </summary>
            public int HighMemoryPressureClearPercentage { get; set; } = 50;
        }
    }
}
