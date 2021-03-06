﻿// Copyright 2017 Eric Millin
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Grib.Api.Interop.SWIG;

namespace Grib.Api.Interop
{
    /// <summary>
    /// A subdomain of field measurements.
    /// </summary>
    public class GribBox
    {
        private GribPoints _points;

        /// <summary>
        /// Initializes a new instance of the <see cref="GribBox"/> class.
        /// </summary>
        /// <param name="msgHandle">The MSG handle.</param>
        /// <param name="nw">The nw.</param>
        /// <param name="se">The se.</param>
        public GribBox (GribHandle msgHandle, GridCoordinate nw, GridCoordinate se)
        {
            int err;
            var box = GribApiProxy.GribBoxNew(msgHandle, out err);

            if (err != 0)
            {
                throw GribApiException.Create(err);
            }

            var pts = GribApiProxy.GribBoxGetPoints(box, nw.Latitude, nw.Longitude, se.Latitude, se.Longitude, out err);

            if (err != 0)
            {
                throw GribApiException.Create(err);
            }

            _points = new GribPoints(SWIGTYPE_p_grib_points.getCPtr(pts).Handle, false);
        }

        /// <summary>
        /// Gets or sets the latitudes.
        /// </summary>
        /// <value>
        /// The latitudes.
        /// </value>
        public double[] Latitudes
        {
            set
            {
                _points.latitudes = value;
            }
            get
            {
                return _points.latitudes;
            }
        }

        /// <summary>
        /// Gets or sets the longitudes.
        /// </summary>
        /// <value>
        /// The longitudes.
        /// </value>
        public double[] Longitudes
        {
            set
            {
                _points.longitudes = value;
            }
            get
            {
                return _points.longitudes;
            }
        }

        /// <summary>
        /// Gets or sets the indexes.
        /// </summary>
        /// <value>
        /// The indexes.
        /// </value>
        public uint Indexes
        {
            set
            {
                _points.indexes = value;
            }
            get
            {
                return _points.indexes;
            }
        }

        /// <summary>
        /// Gets or sets the group start.
        /// </summary>
        /// <value>
        /// The group start.
        /// </value>
        public uint GroupStart
        {
            set
            {
                _points.groupStart = value;
            }
            get
            {
                return _points.groupStart;
            }
        }

        /// <summary>
        /// Gets or sets the length of the group.
        /// </summary>
        /// <value>
        /// The length of the group.
        /// </value>
        public uint GroupLength
        {
            set
            {
                _points.groupLen = value;
            }
            get
            {
                return _points.groupLen;
            }
        }

        /// <summary>
        /// Gets or sets the group count.
        /// </summary>
        /// <value>
        /// The group count.
        /// </value>
        public uint GroupCount
        {
            set
            {
                _points.nGroups = value;
            }
            get
            {
                return _points.nGroups;
            }
        }

        /// <summary>
        /// Gets or sets the count.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public uint Count
        {
            set
            {
                _points.n = value;
            }
            get
            {
                return _points.n;
            }
        }

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>
        /// The size.
        /// </value>
        public uint Size
        {
            set
            {
                _points.size = value;
            }
            get
            {
                return _points.size;
            }
        }
    }
}
