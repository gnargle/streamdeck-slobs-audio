var websocket = null,
    uuid = null,
    actionInfo = {},
    inInfo = {},
    runningApps = [],
    isQT = navigator.appVersion.includes('QtWebEngine');


  const PORT = 59650,
  		url = `http://127.0.0.1:${PORT}/api`;
  var audioSources = [],
  requests = [],
  connectionStatus = 'disconnected',
  nextRequestId = 1,
  socket = null;

  function doConnect() {
  	if (connectionStatus !== 'disconnected') return;
    connectionStatus = 'pending';
    socket = new SockJS(url);

    socket.onopen = () => {
      console.log('open');
      onConnectionHandler();
	  var select_single = document.getElementById('select_single');
	  select_single.disabled = false;
	  var disconnect_warn = document.getElementById('disconnect_warn');
	  disconnect_warn.style.visibility = "hidden";
    };

    socket.onmessage = (e) => {
      onMessageHandler(e.data);
    };


    socket.onclose = (e) => {
      connectionStatus = 'disconnected';
    };    
  }

  function request(resourceId, methodName, ...args) {
    let id = nextRequestId++;
    let requestBody = {
      jsonrpc: '2.0',
      id,
      method: methodName,
      params: { resource: resourceId, args }
    };

    return sendMessage(requestBody);
  }
  function sendMessage(message) {
    let requestBody = message;
    if (typeof message === 'string') {
      try {
        requestBody = JSON.parse(message);
      } catch (e) {
        alert('Invalid JSON');
        return;
      }
    }

    if (!requestBody.id) {
      alert('id is required');
      return;
    }

    return new Promise((resolve, reject) => {
      requests[requestBody.id] = {
        body: requestBody,
        resolve,
        reject,
        completed: false
      };
      this.socket.send(JSON.stringify(requestBody));
    });
  }

  function onConnectionHandler() {
    connectionStatus = 'connected';
    request("AudioService", "getSources").then(audioSources => {
    	  	var select_single = document.getElementById('select_single');
    	  	var i = 1;
			while (select_single.length > 0) {
			  select_single.remove(select_single.length-1);
			}
	        audioSources.forEach(source => {
	          	var option = document.createElement("option");
	          	option.value = source.resourceId;
	          	option.text = source.name;
	        	select_single.add(option);
	        });
	        refreshSettings(actionInfo.payload.settings);
        }); 
  }

  function onMessageHandler(data) {
    let message = JSON.parse(data);
    let request = requests[message.id];

    if (request) {
      if (message.error) {
        request.reject(message.error);
      } else {
        request.resolve(message.result);
      }
      delete requests[message.id];
    }

    const result = message.result;
    if (!result) return;

  }

function connectElgatoStreamDeckSocket(inPort, inUUID, inRegisterEvent, inInfo, inActionInfo) {
    uuid = inUUID;
    actionInfo = JSON.parse(inActionInfo); // cache the info
    inInfo = JSON.parse(inInfo);
    websocket = new WebSocket('ws://localhost:' + inPort);

    addDynamicStyles(inInfo.colors);    
    
    websocket.onopen = function () {
        var register = {
            event: inRegisterEvent,
            uuid: inUUID
        };

        websocket.send(JSON.stringify(register));
    };

    websocket.onmessage = function (evt) {
        // Received message from Stream Deck
        var jsonObj = JSON.parse(evt.data);
        switch (jsonObj.event) {
            case 'didReceiveSettings':
                refreshSettings(jsonObj.payload.settings);
                break;
            case 'propertyInspectorDidDisappear':
                updateSettings();
                break;
            default:
                break;
        }
    };
    doConnect();    
}

function refreshSettings(settings) {
    var select_single = document.getElementById('select_single');

    if (settings) {
        select_single.value = settings.deviceName;
    }
}

function updateSettings() {
    var select_single = document.getElementById('select_single');

    var setSettings = {};
    setSettings.event = 'setSettings';
    setSettings.context = uuid;
    setSettings.payload = {};
    setSettings.payload.deviceName = select_single.value;

    websocket.send(JSON.stringify(setSettings));
}

if (!isQT) {
    document.addEventListener('DOMContentLoaded', function () {
        initPropertyInspector();
    });
}

function addDynamicStyles(clrs) {
    const node = document.getElementById('#sdpi-dynamic-styles') || document.createElement('style');
    if (!clrs.mouseDownColor) clrs.mouseDownColor = fadeColor(clrs.highlightColor, -100);
    const clr = clrs.highlightColor.slice(0, 7);
    const clr1 = fadeColor(clr, 100);
    const clr2 = fadeColor(clr, 60);
    const metersActiveColor = fadeColor(clr, -60);

    node.setAttribute('id', 'sdpi-dynamic-styles');
    node.innerHTML = `

    input[type="radio"]:checked + label span,
    input[type="checkbox"]:checked + label span {
        background-color: ${clrs.highlightColor};
    }

    input[type="radio"]:active:checked + label span,
    input[type="radio"]:active + label span,
    input[type="checkbox"]:active:checked + label span,
    input[type="checkbox"]:active + label span {
      background-color: ${clrs.mouseDownColor};
    }

    input[type="radio"]:active + label span,
    input[type="checkbox"]:active + label span {
      background-color: ${clrs.buttonPressedBorderColor};
    }

    td.selected,
    td.selected:hover,
    li.selected:hover,
    li.selected {
      color: white;
      background-color: ${clrs.highlightColor};
    }

    .sdpi-file-label > label:active,
    .sdpi-file-label.file:active,
    label.sdpi-file-label:active,
    label.sdpi-file-info:active,
    input[type="file"]::-webkit-file-upload-button:active,
    button:active {
      background-color: ${clrs.buttonPressedBackgroundColor};
      color: ${clrs.buttonPressedTextColor};
      border-color: ${clrs.buttonPressedBorderColor};
    }

    ::-webkit-progress-value,
    meter::-webkit-meter-optimum-value {
        background: linear-gradient(${clr2}, ${clr1} 20%, ${clr} 45%, ${clr} 55%, ${clr2})
    }

    ::-webkit-progress-value:active,
    meter::-webkit-meter-optimum-value:active {
        background: linear-gradient(${clr}, ${clr2} 20%, ${metersActiveColor} 45%, ${metersActiveColor} 55%, ${clr})
    }
    `;
    document.body.appendChild(node);
};

/** UTILITIES */

/*
    Quick utility to lighten or darken a color (doesn't take color-drifting, etc. into account)
    Usage:
    fadeColor('#061261', 100); // will lighten the color
    fadeColor('#200867'), -100); // will darken the color
*/
function fadeColor(col, amt) {
    const min = Math.min, max = Math.max;
    const num = parseInt(col.replace(/#/g, ''), 16);
    const r = min(255, max((num >> 16) + amt, 0));
    const g = min(255, max((num & 0x0000FF) + amt, 0));
    const b = min(255, max(((num >> 8) & 0x00FF) + amt, 0));
    return '#' + (g | (b << 8) | (r << 16)).toString(16).padStart(6, 0);
}
