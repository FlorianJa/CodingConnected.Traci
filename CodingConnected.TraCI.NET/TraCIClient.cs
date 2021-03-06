﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using CodingConnected.TraCI.NET.Commands;
using CodingConnected.TraCI.NET.Helpers;

#if NLOG
using NLog;
#endif

namespace CodingConnected.TraCI.NET
{
    /// <summary>
    /// A simple )and yet incomplete) client-side implementation of TraCI, for using SUMO
    /// with .NET applications.
    /// </summary>
    public class TraCIClient
    {

        #if NLOG
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        #endif

        #region Fields

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly byte[] _receiveBuffer = new byte[32768];
	    private ControlCommands _control;
	    private InductionLoopCommands _inductionLoop;
	    private LaneAreaDetectorCommands _laneAreaDetector;
	    private MultiEntryExitDetectorCommands _multiEntryExitDetector;
	    private LaneCommands _lane;
	    private TrafficLightCommands _trafficLight;
	    private VehicleCommands _vehicle;
	    private PersonCommands _person;
	    private VehicleTypeCommands _vehicleType;
	    private RouteCommands _route;
	    private POICommands _POI;
	    private PolygonCommands _polygon;
	    private GuiCommands _gui;
	    private JunctionCommands _junction;
	    private EdgeCommands _edge;
	    private SimulationCommands _simulation;

	    #endregion // Fields

        #region Properties

	    public ControlCommands Control => _control ?? (_control = new ControlCommands(this));

	    public InductionLoopCommands InductionLoop => _inductionLoop ?? (_inductionLoop = new InductionLoopCommands(this));

	    public LaneAreaDetectorCommands LaneAreaDetector => _laneAreaDetector ?? (_laneAreaDetector = new LaneAreaDetectorCommands(this));

	    public MultiEntryExitDetectorCommands MultiEntryExitDetector => _multiEntryExitDetector ?? (_multiEntryExitDetector = new MultiEntryExitDetectorCommands(this));

	    public LaneCommands Lane => _lane ?? (_lane = new LaneCommands(this));

	    public TrafficLightCommands TrafficLight => _trafficLight ?? (_trafficLight = new TrafficLightCommands(this));

	    public VehicleCommands Vehicle => _vehicle ?? (_vehicle = new VehicleCommands(this));

	    public PersonCommands Person => _person ?? (_person = new PersonCommands(this));
	    
	    public VehicleTypeCommands VehicleType => _vehicleType ?? (_vehicleType = new VehicleTypeCommands(this));

		public RouteCommands Route => _route ?? (_route = new RouteCommands(this));

		public POICommands POI => _POI ?? (_POI = new POICommands(this));

		public PolygonCommands Polygon => _polygon ?? (_polygon = new PolygonCommands(this));

	    public JunctionCommands Junction => _junction ?? (_junction = new JunctionCommands(this));

	    public EdgeCommands Edge => _edge ?? (_edge = new EdgeCommands(this));

	    public SimulationCommands Simulation => _simulation ?? (_simulation = new SimulationCommands(this));

	    public GuiCommands Gui => _gui ?? (_gui = new GuiCommands(this));

	    #endregion // Properties

        #region Public Methods

        /// <summary>
        /// Connects to the SUMO server instance
        /// </summary>
        /// <param name="hostname">Hostname or ip address where SUMO is running</param>
        /// <param name="port">Port at which SUMO exposes the API</param>
        public async Task ConnectAsync(string hostname, int port)
        {
            _client = new TcpClient
            {
                ReceiveBufferSize = 32768,
                SendBufferSize = 32768
            };
            await _client.ConnectAsync(hostname, port);
            _stream = _client.GetStream();
        }


        public bool Connect(string hostname, int port)
        {
            _client = new TcpClient
            {
                ReceiveBufferSize = 32768,
                SendBufferSize = 32768
            };

            try
            {
                _client.Connect(hostname, port);
            }
            catch (Exception)
            {
                return false;
            }

            if (_client.Connected)
            {
                _stream = _client.GetStream();
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion // Public Methods

        #region Set Variable Methods

        /// <summary>
        /// Sets the state of all lights of a controlled junction
        /// </summary>
        /// <param name="trafficLightId">The id of the traffic light as set in SUMO</param>
        /// <param name="state">The state to set the traffic lights to, parsed as a string, as is 
        /// described here: http://sumo.dlr.de/wiki/TraCI/Change_Traffic_Lights_State </param>
        public void SetTrafficLightState(string trafficLightId, string state)
        {
            var command = new TraCICommand { Identifier = TraCIConstants.CMD_SET_TL_VARIABLE };
            var bytes = new List<byte> { TraCIConstants.TL_RED_YELLOW_GREEN_STATE };
            bytes.AddRange(TraCIDataConverter.GetTraCIBytesFromASCIIString(trafficLightId));
            bytes.Add(TraCIConstants.TYPE_STRING);
            bytes.AddRange(BitConverter.GetBytes(state.Length).Reverse());
            bytes.AddRange(Encoding.ASCII.GetBytes(state));

            command.Contents = bytes.ToArray();
            // ReSharper disable once UnusedVariable
            var response = SendMessage(command);
        }

        public void SetSingleTrafficLightState(string trafficLightId, string signalgroup, char singlestate)
        {
            var command = new TraCICommand { Identifier = TraCIConstants.CMD_SET_TL_VARIABLE };
            var bytes = new List<byte> { TraCIConstants.TL_RED_YELLOW_GREEN_SINGLESTATE };
            bytes.AddRange(TraCIDataConverter.GetTraCIBytesFromASCIIString(trafficLightId));
            bytes.Add(TraCIConstants.TYPE_STRING);
            bytes.AddRange(BitConverter.GetBytes((signalgroup + ' ' + singlestate).Length).Reverse());
            bytes.AddRange(Encoding.ASCII.GetBytes(signalgroup + ' ' + singlestate));

            command.Contents = bytes.ToArray();
            // ReSharper disable once UnusedVariable
            var response = SendMessage(command);
        }

		#endregion // Set Variable Methods

		#region Public Methods

	    internal TraCIResult[] SendMessage(TraCICommand command)
	    {
		    if (!_client.Connected)
		    {
			    return null;
		    }

		    var msg = TraCIDataConverter.GetMessageBytes(command);
		    _client.Client.Send(msg);
		    try
		    {
			    var bytesRead = _stream.Read(_receiveBuffer, 0, 32768);
			    if (bytesRead < 0)
			    {
				    // Read returns 0 if the client closes the connection
				    throw new IOException();
			    }
			    var response = _receiveBuffer.Take(bytesRead).ToArray();
#if NLOG
                _logger.Trace(" << {0}", BitConverter.ToString(response));
#endif
			    var trresponse = TraCIDataConverter.HandleResponse(response);
			    return trresponse?.Length > 0 ? trresponse : null;
		    }
		    catch
		    {
			    return null; // TODO
		    }
	    }

		#endregion // Public Methods

		#region Private Methods

		#endregion // Private Methods

		#region Constructor

		public TraCIClient()
	    {
	    }

	    #endregion // Constructor
    }
}
