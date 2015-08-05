////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;

namespace Holofunk.StateMachines
{
    /// <summary>A model can be updated.</summary>
    /// <remarks>Lets the current model define the current update function to be applied to it at the current moment..</remarks>
    public abstract class Model
    {
        /// <summary>
        /// Update the model at the current moment.
        /// </summary>
        /// <remarks>
        /// [GameThread]
        /// </remarks>
        public abstract void GameUpdate(Moment now);
    }
}
