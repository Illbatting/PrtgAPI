﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PrtgAPI.Parameters;
using PrtgAPI.PowerShell.Base;
using PrtgAPI.Request;

namespace PrtgAPI.PowerShell.Cmdlets
{
    /// <summary>
    /// <para type="synopsis">Retrieves objects from a PRTG Server.</para>
    /// 
    /// <para type="description">The Get-Object cmdlet retrieves objects from a PRTG Server. Get-Object is capable of retrieving
    /// any object with a unique identifier within the PRTG object hierarchy, including internal system nodes.</para>
    /// 
    /// <para type="description">By default, any objects returned by the Get-Object cmdlet are returned in their raw <see cref="PrtgObject"/> state.
    /// For objects that are natively supported by PrtgAPI's type system, these objects can be transformed into their "true" forms by specifying
    /// the -Resolve parameter. When specified, Get-Object will group objects of a similar type up together to re-retrieve them via as few
    /// API calls as possible. As objects must effectively be retrieved twice, care should be taken when attempting to resolve a large number of objects.</para>
    /// 
    /// <para type="description">Objects returned by Get-Object can be limited those of a particular <see cref="ObjectType"/> by specifying the -<see cref="Type"/>
    /// parameter. When filtering by sensors, the underlying raw type name must be specified (e.g. "ping", "wmivolume"). If the generic
    /// object type <see cref="ObjectType.Sensor"/> is specified, Get-Object will refrain from filtering by type server side,
    /// such that the complete type of each object may be inspected client side.</para>
    /// 
    /// <example>
    ///     <code>C:\> Get-Object</code>
    ///     <para>Retrieve all uniquely identifiable objects.</para>
    ///     <para/>
    /// </example>
    /// <example>
    ///     <code>C:\> Get-Object -Id 810</code>
    ///     <para>Retrieve the object with ID 810 (Web Server Options).</para>
    ///     <para/>
    /// </example>
    /// <example>
    ///     <code>C:\> Get-Object *report*</code>
    ///     <para>Retrieve all objects whose name contains "report".</para>
    ///     <para/>
    /// </example>
    /// <example>
    ///     <code>C:\> Get-Object -Type Device,System</code>
    ///     <para>Retrieve all Device and System objects.</para>
    ///     <para/>
    /// </example>
    /// <example>
    ///     <code>C:\> Get-Object -Type wmivolume -Resolve</code>
    ///     <para>Get all WMI Volume sensor objects and resolve them to objects of type <see cref="Sensor"/>.</para>
    /// </example>
    /// 
    /// <para type="link">Get-Sensor</para>
    /// <para type="link">Get-Device</para>
    /// <para type="link">Get-Group</para>
    /// <para type="link">Get-Probe</para>
    /// <para type="link">Get-NotificationAction</para>
    /// <para type="link">Get-PrtgSchedule</para>
    /// <para type="link">Get-ObjectProperty</para>
    /// <para type="link">Set-ObjectProperty</para>
    /// </summary>
    [OutputType(typeof(PrtgObject))]
    [Cmdlet(VerbsCommon.Get, "Object")]
    public class GetObject : PrtgTableFilterCmdlet<PrtgObject, PrtgObjectParameters>
    {
        /// <summary>
        /// <para type="description">Specifies that returned objects should be resolved to their most derived object types (<see cref="Sensor"/>, <see cref="Device"/>, <see cref="Probe"/>, etc.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Resolve { get; set; }

        /// <summary>
        /// <para type="description">Filter results to those of a specified <see cref="ObjectType"/>.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public StringEnum<ObjectType>[] Type { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetObject"/> class.
        /// </summary>
        public GetObject() : base(Content.Objects, true)
        {
        }

        /// <summary>
        /// Process any post retrieval filters specific to the current cmdlet.
        /// </summary>
        /// <param name="records">The records to filter.</param>
        /// <returns>The filtered records.</returns>
        protected override IEnumerable<PrtgObject> PostProcessAdditionalFilters(IEnumerable<PrtgObject> records)
        {
            records = FilterResponseRecordsByType(records);

            if (Resolve)
                return LiftObjects(records);

            return base.PostProcessAdditionalFilters(records).OrderBy(o => o.Id);
        }

        private IEnumerable<PrtgObject> FilterResponseRecordsByType(IEnumerable<PrtgObject> records)
        {
            if (Type != null)
                records = records.Where(r => Type.Any(t => r.Type == t));

            return records;
        }

        private IEnumerable<PrtgObject> LiftObjects(IEnumerable<PrtgObject> objects)
        {
            var grouped = objects.GroupBy(d => d.Type);

            foreach (var group in grouped)
            {
                switch (group.Key.Value)
                {
                    case ObjectType.Sensor:
                        foreach (var s in Sensors(group))
                            yield return s;
                        break;
                    case ObjectType.Device:
                        foreach (var d in Devices(group))
                            yield return d;
                        break;
                    case ObjectType.Group:
                        foreach (var g in Groups(group))
                            yield return g;
                        break;
                    case ObjectType.Probe:
                        foreach (var p in Probes(group))
                            yield return p;
                        break;
                    case ObjectType.Notification:
                        foreach (var n in Notifications(group))
                            yield return n;
                        break;
                    case ObjectType.Schedule:
                        foreach (var s in Schedules(group))
                            yield return s;
                        break;
                    default:
                        foreach (var g in group)
                            yield return g;
                        break;
                }
            }
        }

        #region Lift

        private IEnumerable<Sensor> Sensors(IGrouping<StringEnum<ObjectType>, PrtgObject> group) =>
            StreamLift<Sensor, SensorParameters>(f => new SensorParameters(f), group);

        private IEnumerable<Device> Devices(IGrouping<StringEnum<ObjectType>, PrtgObject> group) =>
            StreamLift<Device, DeviceParameters>(f => new DeviceParameters(f), group);

        private IEnumerable<Group> Groups(IGrouping<StringEnum<ObjectType>, PrtgObject> group) =>
            StreamLift<Group, GroupParameters>(f => new GroupParameters(f), group);

        private IEnumerable<Probe> Probes(IGrouping<StringEnum<ObjectType>, PrtgObject> group) =>
            StreamLift<Probe, ProbeParameters>(f => new ProbeParameters(f), group);

        private IEnumerable<NotificationAction> Notifications(IGrouping<StringEnum<ObjectType>, PrtgObject> group) =>
            client.GetNotificationActions(Property.Id, group.Select(g => g.Id));

        private IEnumerable<Schedule> Schedules(IGrouping<StringEnum<ObjectType>, PrtgObject> group) =>
            client.GetSchedules(Property.Id, group.Select(g => g.Id));

        #endregion

        private IEnumerable<TObject> StreamLift<TObject, TParam>(Func<SearchFilter, TParam> getParams, IGrouping<StringEnum<ObjectType>, PrtgObject> group)
            where TObject : IObject
            where TParam : ContentParameters<TObject>, IShallowCloneable<TParam>
        {
            var manager = new StreamManager<TObject, TParam>(
                client.ObjectEngine,                                               //Object Engine
                getParams(new SearchFilter(Property.Id, group.Select(e => e.Id))), //Parameters
                null,
                true
            );

            var objs = client.ObjectEngine.SerialStreamObjectsInternal(manager);

            return objs;
        }

        /// <summary>
        /// Processes additional parameters specific to the current cmdlet.
        /// </summary>
        protected override void ProcessAdditionalParameters()
        {
            if (Type != null)
            {
                //If any Type specified ObjectType.Sensor with no underlying type,
                //perform no filtering, as we'll need to return everything to evaluate
                //types client side.
                if (Type.All(t => t.StringValue.ToLower() != "sensor"))
                    AddPipelineFilter(Property.Type, Type);
            }

            base.ProcessAdditionalParameters();
        }

        /// <summary>
        /// Creates a new parameter object to be used for retrieving generic objects from a PRTG Server.
        /// </summary>
        /// <returns>The default set of parameters.</returns>
        protected override PrtgObjectParameters CreateParameters() => new PrtgObjectParameters();
    }
}
