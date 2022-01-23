using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{





















		#region mdk preserve
		// === CONFIG ===


		// Use name of grid (under Info) if this is empty.
		string OverrideRunwayName = "";

		// Power saving mode for when camera is not active.
		// Enable this if there are other scripts using raycasting.
		bool LeaveRaycastOn = false;

		
		#endregion

		const string BadSaveMsg = "Warning: Malformed save data. Please redo.";
		static readonly char[] newlineChars = { '\r', '\n' };
		static readonly Vector3D zerovec = new Vector3D(0, 0, 0);
		Vector3D start = zerovec;
		Vector3D end = zerovec;
		long eid = 0;
		//MatrixD world = new MatrixD();
		IMyGridTerminalSystem G;
		List<IMyCameraBlock> cams = new List<IMyCameraBlock>();

		public Program()
		{
			G = GridTerminalSystem;
			//cam = G.GetBlockWithName(CameraBlock) as IMyCameraBlock;
			//
			//if (cam != null)
			//{
			//	cam.EnableRaycast = true;
			//}

			foreach (var lineRaw in Storage.Split(newlineChars, StringSplitOptions.RemoveEmptyEntries))
			{
				var line = lineRaw.Trim();
				if (line.StartsWith("#") || line.StartsWith("//")) { continue; }
				var sp = line.Split('=');
				if (sp.Length != 2)
				{
					Echo(BadSaveMsg);
				}
				switch (sp[0])
				{
					case "start":
						if (!Vector3D.TryParse(sp[1], out start))
						{
							Echo(BadSaveMsg);
						}
						break;
					case "end":
						if (!Vector3D.TryParse(sp[1], out end))
						{
							Echo(BadSaveMsg);
						}
						break;
				}
			}
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
		}

		public void Save()
		{
			Storage = $"start={start}\r\nend={end}\r\neid={eid}";
		}

		//List<IMyRadioAntenna> ants = new List<IMyRadioAntenna>();
		public void Main(string argument, UpdateType updateSource)
		{

			switch (updateSource)
			{
				case UpdateType.Update100:
				case UpdateType.Update10:
				case UpdateType.Update1:
					G.GetBlocksOfType(cams);
					foreach (var c in cams)
					{
						if (c.IsActive) { c.EnableRaycast = true; }
						else if (!LeaveRaycastOn) { c.EnableRaycast = false; }
					}

					if (start == zerovec || end == zerovec)
					{
						Echo("Setup not complete. Please see instructions in script.");
						return;
					}

					//G.GetBlocksOfType(ants);
					//IMyRadioAntenna ant = null;
					//foreach (var a in ants)
					//{
					//	if (a.CustomName.ToLower().Contains("ils"))
					//	{
					//		ant = a;
					//		break;
					//	}
					//}
					//if (ant== null)
					//{
					//	Echo("No antenna");
					//	return;
					//}
					//ant.EnableBroadcasting = true;

					Vector3D startWorld = mul(Me.WorldMatrix, start);
					Vector3D endWorld = mul(Me.WorldMatrix, end);
					string gridname = OverrideRunwayName.Length > 0 ? OverrideRunwayName : Me.CubeGrid.CustomName;
					string msg = $"start={startWorld.X},{startWorld.Y},{startWorld.Z}\r\nend={endWorld.X},{endWorld.Y},{endWorld.Z}\r\ngridname={gridname}";
					IGC.SendBroadcastMessage("ILS", msg);
					Save();
					Echo($"{gridname}\r\nLast squawk at {DateTime.Now}");
					break;
				default:
					switch (argument)
					{
						case "start":
							start = getRelativeCoords() ?? zerovec;
							break;
						case "end":
							end = getRelativeCoords() ?? zerovec;
							break;
						default:
							Echo("Unknown command");
							break;
					}
					break;
			}
		}

		Vector3D? getRelativeCoords()
		{
			IMyCameraBlock cam = null;
			G.GetBlocksOfType(cams);
			foreach (var c in cams)
			{
				if (c.IsActive)
				{
					cam = c;
					break;
				}
			}
			if (cam == null)
			{
				Echo("Use camera for 'start' and 'end' commands.");
				return null;
			}
			Echo($"{cam.AvailableScanRange}m");
			var ent = cam.Raycast(cam.AvailableScanRange - 1.0);
			
			if (ent.IsEmpty())
			{
				Echo("No entity found where you pointed the camera.");
				return null;
			}
			eid = ent.EntityId;
			//return div((ent.HitPosition ?? zerovec) - Me.WorldMatrix.Translation, Me.WorldMatrix);
			return div((ent.HitPosition ?? zerovec), Me.WorldMatrix);
		}

		Vector3D mul(MatrixD m, Vector3D v)
		{
			return new Vector3(
				(m.M11 * v.X) + (m.M21 * v.Y) + (m.M31 * v.Z) + m.M41,
				(m.M12 * v.X) + (m.M22 * v.Y) + (m.M32 * v.Z) + m.M42,
				(m.M13 * v.X) + (m.M23 * v.Y) + (m.M33 * v.Z) + m.M43);
		}
		Vector3D div(Vector3D v, MatrixD m)
		{
			return mul(Inverse(m), v);
		}

		MatrixD Inverse(MatrixD m)
		{
			var r = new MatrixD();
			r.M11 = 0
			  + (m.M22 * m.M33 * m.M44) + (m.M32 * m.M43 * m.M24) + (m.M42 * m.M23 * m.M34)
			  - (m.M42 * m.M33 * m.M24) - (m.M32 * m.M23 * m.M44) - (m.M22 * m.M43 * m.M34);
			r.M21 = 0
			  - (m.M21 * m.M33 * m.M44) - (m.M31 * m.M43 * m.M24) - (m.M41 * m.M23 * m.M34)
			  + (m.M41 * m.M33 * m.M24) + (m.M31 * m.M23 * m.M44) + (m.M21 * m.M43 * m.M34);
			r.M31 = 0
			  + (m.M21 * m.M32 * m.M44) + (m.M31 * m.M42 * m.M24) + (m.M41 * m.M22 * m.M34)
			  - (m.M41 * m.M32 * m.M24) - (m.M31 * m.M22 * m.M44) - (m.M21 * m.M42 * m.M34);
			r.M41 = 0
			  - (m.M21 * m.M32 * m.M43) - (m.M31 * m.M42 * m.M23) - (m.M41 * m.M22 * m.M33)
			  + (m.M41 * m.M32 * m.M23) + (m.M31 * m.M22 * m.M43) + (m.M21 * m.M42 * m.M33);

			r.M12 = 0
			  - (m.M12 * m.M33 * m.M44) - (m.M32 * m.M43 * m.M14) - (m.M42 * m.M13 * m.M34)
			  + (m.M42 * m.M33 * m.M14) + (m.M32 * m.M13 * m.M44) + (m.M12 * m.M43 * m.M34);
			r.M22 = 0
			  + (m.M11 * m.M33 * m.M44) + (m.M31 * m.M43 * m.M14) + (m.M41 * m.M13 * m.M34)
			  - (m.M41 * m.M33 * m.M14) - (m.M31 * m.M13 * m.M44) - (m.M11 * m.M43 * m.M34);
			r.M32 = 0
			  - (m.M11 * m.M32 * m.M44) - (m.M31 * m.M42 * m.M14) - (m.M41 * m.M12 * m.M34)
			  + (m.M41 * m.M32 * m.M14) + (m.M31 * m.M12 * m.M44) + (m.M11 * m.M42 * m.M34);
			r.M42 = 0
			  + (m.M11 * m.M32 * m.M43) + (m.M31 * m.M42 * m.M13) + (m.M41 * m.M12 * m.M33)
			  - (m.M41 * m.M32 * m.M13) - (m.M31 * m.M12 * m.M43) - (m.M11 * m.M42 * m.M33);

			r.M13 = 0
			  + (m.M12 * m.M23 * m.M44) + (m.M22 * m.M43 * m.M14) + (m.M42 * m.M13 * m.M24)
			  - (m.M42 * m.M23 * m.M14) - (m.M22 * m.M13 * m.M44) - (m.M12 * m.M43 * m.M24);
			r.M23 = 0
			  - (m.M11 * m.M23 * m.M44) - (m.M21 * m.M43 * m.M14) - (m.M41 * m.M13 * m.M24)
			  + (m.M41 * m.M23 * m.M14) + (m.M21 * m.M13 * m.M44) + (m.M11 * m.M43 * m.M24);
			r.M33 = 0
			  + (m.M11 * m.M22 * m.M44) + (m.M21 * m.M42 * m.M14) + (m.M41 * m.M12 * m.M24)
			  - (m.M41 * m.M22 * m.M14) - (m.M21 * m.M12 * m.M44) - (m.M11 * m.M42 * m.M24);
			r.M43 =
			  -(m.M11 * m.M22 * m.M43) - (m.M21 * m.M42 * m.M13) - (m.M41 * m.M12 * m.M23)
			  + (m.M41 * m.M22 * m.M13) + (m.M21 * m.M12 * m.M43) + (m.M11 * m.M42 * m.M23);

			r.M14 = 0
			  - (m.M12 * m.M23 * m.M34) - (m.M22 * m.M33 * m.M14) - (m.M32 * m.M13 * m.M24)
			  + (m.M32 * m.M23 * m.M14) + (m.M22 * m.M13 * m.M34) + (m.M12 * m.M33 * m.M24);
			r.M24 = 0
			  + (m.M11 * m.M23 * m.M34) + (m.M21 * m.M33 * m.M14) + (m.M31 * m.M13 * m.M24)
			  - (m.M31 * m.M23 * m.M14) - (m.M21 * m.M13 * m.M34) - (m.M11 * m.M33 * m.M24);
			r.M34 = 0
			  - (m.M11 * m.M22 * m.M34) - (m.M21 * m.M32 * m.M14) - (m.M31 * m.M12 * m.M24)
			  + (m.M31 * m.M22 * m.M14) + (m.M21 * m.M12 * m.M34) + (m.M11 * m.M32 * m.M24);
			r.M44 = 0
			  + (m.M11 * m.M22 * m.M33) + (m.M21 * m.M32 * m.M13) + (m.M31 * m.M12 * m.M23)
			  - (m.M31 * m.M22 * m.M13) - (m.M21 * m.M12 * m.M33) - (m.M11 * m.M32 * m.M23);

			return r;
		}













































	}
}
