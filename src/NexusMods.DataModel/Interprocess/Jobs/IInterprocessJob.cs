﻿using System.ComponentModel;
using NexusMods.DataModel.Abstractions;
using NexusMods.DataModel.Abstractions.Ids;
using NexusMods.DataModel.Interprocess.Jobs;
using NexusMods.DataModel.Loadouts;
using NexusMods.DataModel.RateLimiting;

namespace NexusMods.DataModel.Interprocess.Messages;

public interface IInterprocessJob : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// The OS level processId of the process that created the job. If this process
    /// cannot be found, the job will be considered orphaned and will be removed.
    /// </summary>
    public ProcessId ProcessId { get; }

    /// <summary>
    /// Unique identifier of the job.
    /// </summary>
    public JobId JobId { get; }

    /// <summary>
    /// How far along the job is.
    /// </summary>
    public Percent Progress { get; set; }

    /// <summary>
    /// The time the job was started.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// The payload of the job.
    /// </summary>
    public Entity Payload { get; }

}
