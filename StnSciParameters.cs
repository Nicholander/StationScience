/*
    This file is part of Station Science.

    Station Science is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Station Science is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Station Science.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using KSP.Localization;

namespace StationScience.Contracts.Parameters
{
    using static StnSciScenario;

    public interface IPartRelated
    {
        AvailablePart GetPartType();
    }

    public interface IBodyRelated
    {
        CelestialBody GetBody();
    }

    public class StnSciParameter : ContractParameter, IPartRelated, IBodyRelated
    {
        AvailablePart experimentType;
        CelestialBody targetBody;

        public AvailablePart GetPartType()
            => experimentType;
        public CelestialBody GetBody()
            => targetBody;

        public StnSciParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        public StnSciParameter(AvailablePart type, CelestialBody body)
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
            this.experimentType = type;
            this.targetBody = body;
            this.AddParameter(new Parameters.DoExperimentParameter(), null);
            this.AddParameter(new Parameters.ReturnExperimentParameter(), null);
        }

        protected override string GetHashString()
            => experimentType.name;

        protected override string GetTitle()
            => Localizer.Format("#autoLOC_StatSciParam_Title", experimentType.title, targetBody.GetDisplayName());

        protected override string GetNotes()
            => Localizer.Format("#autoLOC_StatSciParam_Notes", experimentType.title, targetBody.GetDisplayName());

        private bool SetExperiment(string exp)
        {
            experimentType = PartLoader.getPartInfoByName(exp);
            if (experimentType == null)
            {
                LogError("Couldn't find experiment part: " + exp);
                return false;
            }
            return true;
        }

        public void Complete()
            => SetComplete();

        private bool SetTarget(string planet)
        {
            targetBody = FlightGlobals.Bodies.FirstOrDefault(body => body.bodyName.ToLower() == planet.ToLower());
            if (targetBody == null)
            {
                LogError("Couldn't find planet: " + planet);
                return false;
            }
            return true;
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("targetBody", targetBody.name);
            node.AddValue("experimentType", experimentType.name);
        }
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
            string expID = node.GetValue("experimentType");
            SetExperiment(expID);
            string bodyID = node.GetValue("targetBody");
            SetTarget(bodyID);
        }

        static public AvailablePart getExperimentType(ContractParameter o)
            => ((o.Parent ?? o.Root) as IPartRelated)?.GetPartType();
        static public CelestialBody getTargetBody(ContractParameter o)
            => (o.Parent as IBodyRelated)?.GetBody();
    }

    public class DoExperimentParameter : ContractParameter
    {
        public DoExperimentParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        protected override string GetHashString()
            => Localizer.Format("#autoLOC_StatSciDoExp_Hash", this.GetHashCode());
        protected override string GetTitle()
        {
            var targetBody = StnSciParameter.getTargetBody(this);
            return targetBody == null
                ? Localizer.Format("#autoLOC_StatSciDoExp_TitleA")
                : Localizer.Format("#autoLOC_StatSciDoExp_TitleB", targetBody.GetDisplayName());
        }

        private float lastUpdate = 0;

        protected override void OnUpdate()
        {
            base.OnUpdate();

            var now = Time.realtimeSinceStartup;
            if ((now - lastUpdate) < 0.1f)
                return;

            var targetBody = StnSciParameter.getTargetBody(this);
            var experimentType = StnSciParameter.getExperimentType(this);
            if (targetBody == null || experimentType == null)
                return;

            lastUpdate = now;
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
            {
                foreach (var part in vessel.Parts)
                {
                    if (part.name != experimentType.name)
                        continue;
                    var e = part.FindModuleImplementing<StationExperiment>();
                    if (e == null)
                        continue;
                    if (e.completed >= Root.DateAccepted)
                    {
                        ScienceData[] data = e.GetData();
                        foreach (var datum in data)
                        {
                            if (datum.subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                            {
                                SetComplete();
                                return;
                            }
                        }
                    }
                }
            }
            SetIncomplete();
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }

    public class ReturnExperimentParameter : ContractParameter
    {
        public ReturnExperimentParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        protected override string GetHashString()
            => "recover experiment " + this.GetHashCode();
        protected override string GetTitle()
            => Localizer.Format("#autoLOC_StatSciRetParam_Title");

        protected override void OnRegister()
            => GameEvents.onVesselRecovered.Add(OnRecovered);
        protected override void OnUnregister()
            => GameEvents.onVesselRecovered.Remove(OnRecovered);

        private void OnRecovered(ProtoVessel pv, bool dummy)
        {
            var targetBody = StnSciParameter.getTargetBody(this);
            var experimentType = StnSciParameter.getExperimentType(this);
            if (targetBody == null || experimentType == null)
                return;
#if DEBUG
            DebugLog($"Recovering '{pv.vesselName}'; Experiment: '{experimentType.name}'; Body: '{targetBody.name}'");
            var foundPart = false;
#endif
            foreach (var part in pv.protoPartSnapshots)
            {
                if (part.partName != experimentType.name)
                    continue;
#if DEBUG
                foundPart = true;
                var foundModule = false;
#endif
                foreach (var module in part.modules)
                {
                    if (module.moduleName != "StationExperiment")
                        continue;
#if DEBUG
                    foundModule = true;
#endif
                    var cn = module.moduleValues;
                    if (!cn.HasValue("launched"))
                    {
                        DebugLog($"{part.partName}: not launched");
                        continue;
                    }
                    if (!cn.HasValue("completed"))
                    {
                        DebugLog($"{part.partName}: not completed");
                        continue;
                    }
                    float launched, completed;
                    try
                    {
                        launched = float.Parse(cn.GetValue("launched"));
                        completed = float.Parse(cn.GetValue("completed"));
                    }
                    catch (Exception e)
                    {
                        LogError(e.ToString());
                        continue;
                    }
                    if (completed < Root.DateAccepted)
                    {
                        DebugLog($"launched: '{launched}'; accepted: '{Root.DateAccepted}'; completed: '{completed}'");
                        continue;
                    }
                    foreach (var datum in cn.GetNodes("ScienceData"))
                    {
                        if (!datum.HasValue("subjectID"))
                        {
                            LogError("No 'subjectID'");
                            continue;
                        }
                        string subjectID = datum.GetValue("subjectID");
                        if (subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                        {
                            DebugLog("Completed!");
                            SetComplete();
                            if (this.Parent is StnSciParameter parent)
                                parent.Complete();
                            return;
                        }
                        DebugLog($"'{subjectID.ToLower()}' does not contain '{("@" + targetBody.name.ToLower() + "inspace")}'");
                    }
                }
#if DEBUG
                if (!foundModule)
                    DebugLog($"Part '{part.partName}' does not contain module 'StationExperiment'");
#endif
            }
#if DEBUG
            if (!foundPart)
                DebugLog($"No part matching '{experimentType.name}'");
#endif
        }
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }
}
