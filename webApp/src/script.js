import * as d3 from "https://cdn.jsdelivr.net/npm/d3@7/+esm";
import * as THREE from "https://cdn.skypack.dev/three@0.132.2";
import { FBXLoader } from "https://cdn.skypack.dev/three@0.132.2/examples/jsm/loaders/FBXLoader.js";
import {
	GLTFLoader
} from "https://cdn.skypack.dev/three@0.132.2/examples/jsm/loaders/GLTFLoader.js";
import {
	OBJLoader
} from "https://cdn.skypack.dev/three@0.132.2/examples/jsm/loaders/OBJLoader.js";
import {
	OrbitControls
} from "https://cdn.skypack.dev/three@0.132.2/examples/jsm/controls/OrbitControls.js";
import {
	LineGeometry
} from 'https://cdn.skypack.dev/three@0.132.2/examples/jsm/lines/LineGeometry.js';
import {
	LineMaterial
} from 'https://cdn.skypack.dev/three@0.132.2/examples/jsm/lines/LineMaterial.js';
import {
	Line2
} from 'https://cdn.skypack.dev/three@0.132.2/examples/jsm/lines/Line2.js';



let logMode = {
	vrGame: 0,
	immersiveAnalytics: 0,
	infoVisCollab: 1,
	sceneNavigation: 0,
	maintenance: 0,
    videoScene : 0
}

let video = false ;
const animationStep = 100;

let numUsers = 0;
let uniqueUsers ;
let uniqueActions ;
let dynamicColorMapping ;
let x ;
let globalState = {
	currentTimestamp: 0,
	bins: undefined,
  	unit : "minutes",
  	jsonDatas: [],
	avatars: [],
	meshes: [],
	interactionMeshes: [],
	speechMeshes: [],
	intervals: undefined,
	intervalDuration: 0,
	globalStartTime: 0,
	globalEndTime: 0,
	startTimeStamp: 0,
	endTimeStamp: 0,
	currentDataIndex: -1,
	show: Array(numUsers+1).fill(true),
	lineTimeStamp1: 0,
	lineTimeStamp2: 0,
  	finalData: undefined,
	dynamicWidth:0,
	scene: undefined,
	camera:undefined,
	renderer:undefined,
	controls:undefined,
	currentLineSegments : [],
	triangleMesh: [],
	raycastLines : [],
	rightControls: [],
	leftControls : [],
	lineDrawing: [],
	loadedClouds : {},
	loadedObjects : {},
	llmInsightData: {},
	heatmaps : [],
	useCase: "",
	logFIlePath: "",
	llmInsightPath: "",
	objFilePath: "",
	viewProps: {},
	obContext: [],
    markers:{},
    isAnimating: video
};




const configData = await Promise.all([
	fetch('config.json').then(response => response.json()),
]);

let selectedLogMode = Object.keys(logMode).find(key => logMode[key] === 1);
globalState.useCase = selectedLogMode;

if (selectedLogMode && configData[0][selectedLogMode]) {
    globalState.logFIlePath = configData[0][selectedLogMode].logFilePath;
    globalState.llmInsightPath = configData[0][selectedLogMode].llmInsightFilePath;
	globalState.objFilePath = configData[0][selectedLogMode].objFilePath;
	globalState.obContext = configData[0][selectedLogMode].obContext;

} else {
    console.log("No valid mode selected or key not found in JSON.");
}

function initializeViewProps() {
	globalState.viewProps = Object.fromEntries(
	  Array.from({ length: numUsers+1 }, (_, i) => [
		`User${i}`,
		Object.fromEntries(globalState.obContext.map(context => [context, false]))
	  ])
	);
  }


const margin = { top: 20, right: 30, bottom: 5, left: 160 };
let buffer = 167;
const hsl = {
	h: 0,
	s: 0,
	l: 0
};
const userActionColors = {
    "User1": {
        "Action1": "#A8E6CF", // Light Green
        "Action2": "#88DBC2", // Mint Green
        "Action3": "#66CFAE", // Medium Green
        "Action4": "#44C39A", // Green-Teal
        "Action5": "#22B787", // Teal
        "Action6": "#1EA175"  // Dark Teal
    },
    "User2": {
        "Action1": "#4DD0E1", // Light Teal
        "Action2": "#42BACE", // Teal
        "Action3": "#36A1B8", // Deeper Teal
        "Action4": "#2A87A0", // Medium Cyan
        "Action5": "#1F6E88", // Dark Cyan
        "Action6": "#155571"  // Dark Blue-Teal
    },
    "User3": {
        "Action1": "#1E88E5", // Light Blue
        "Action2": "#1876CD", // Medium Blue
        "Action3": "#1462B4", // Darker Blue
        "Action4": "#104F9B", // Navy Blue
        "Action5": "#0D3C82", // Dark Navy
        "Action6": "#09296A"  // Deep Navy
    }
};

const colorScale = d3.scaleOrdinal()
    .domain(["User1", "User2", "User3", "0", "1", "2"])
    .range(["#66CFAE", "#36A1B8", "#4682b4", "#8dd3c7", "#fdcdac", "#bebada"]);


const opacities = [0.2, 0.4, 0.6, 0.8, 1]; //TODO: Remove

function toggleAnimation() {
    if (video) {
        const isPlaying = playIcon.style.display === 'none';

        if (isPlaying) {
            pauseIcon.style.display = 'none';
            playIcon.style.display = 'block';
            globalState.isAnimating = false;
        } else {
            playIcon.style.display = 'none';
            pauseIcon.style.display = 'block';
            globalState.isAnimating = true;
        }

        if (globalState.isAnimating) {
            animateVisualization();
        }
    }
}

export function updateIntervals() {
  createSharedAxis();
  createLines(globalState.lineTimeStamp1, globalState.lineTimeStamp2);
  generateHierToolBar();
  createPlotTemporal();
  plotUserSpecificBarChart();
  plotUserSpecificDurationBarChart();
  updateObjectsBasedOnSelections();
//   plotHeatmap();
  updateMarkersBasedOnSelections();
}

function changeBinSize(newBinSize) {
	const unit = document.querySelector('input[name="unit"]:checked').value;

	var event = new CustomEvent('binSizeChange', {
		detail: { size: newBinSize, unit: unit }
	});
	updateIntervals(newBinSize, unit);
	createPlotTemporal();
	window.dispatchEvent(event);

	callDrawBookmarks(globalState.llmInsightData);
  }

  document.getElementById('binsDropdown').addEventListener('change', function() {
	changeBinSize(this.value);
  });

  document.getElementById('unit-selection-container').addEventListener('change', function() {
	const unit = document.querySelector('input[name="unit"]:checked').value;
	const currentBinSize = document.getElementById('binsDropdown').value;
	changeBinSize(currentBinSize);
	console.log('Unit changed to:', unit);
  });

  window.addEventListener('binSizeChange', function(e) {
	globalState.bins = e.detail.size;
	globalState.unit = e.detail.unit;
	updateIntervals(e.detail.size, e.detail.unit);
	console.log('Bin size changed to:', e.detail.size, 'Unit:', e.detail.unit);
  });

async function loadAvatarModel(filename) {
	const loader = new GLTFLoader();
	const gltf = await loader.loadAsync(filename);
	const avatar = gltf.scene;
	avatar.rotation.set(0, 0, 0);
	avatar.scale.set(1, 1, 1);
	avatar.name = filename;
	globalState.scene.add(avatar);
	return avatar;
}

async function loadIpadModel(filename) {
	const loader = new GLTFLoader();
	const gltf = await loader.loadAsync(filename);
	const avatar = gltf.scene;
	avatar.rotation.set(0, 0, 0);
	avatar.scale.set(2, 2, 2);
	avatar.name = filename;
	globalState.scene.add(avatar);
	return avatar;
}
async function loadHand(filename) {
	const loader = new GLTFLoader();
	const gltf = await loader.loadAsync(filename);
	const avatar = gltf.scene;
	// avatar.rotation.set(0, 0, 0);
	avatar.scale.set(2, 2, 2);
	avatar.name = filename;
	globalState.scene.add(avatar);
	return avatar;
}

function window_onload() {
	generateUserLegends();
	for (let i = 1; i <= numUsers; i++) {
		document.getElementById(`toggle-user${i}`).addEventListener('change', function() {
			const userID = i ;
			globalState.show[userID] = this.checked;
			if (globalState.show[userID]) {
				if (globalState.currentLineSegments[userID]) {
					globalState.scene.add(globalState.currentLineSegments[userID]);
				}
				if (globalState.triangleMesh[userID]) {
					globalState.triangleMesh[userID].forEach(mesh => {
							globalState.scene.add(mesh);
					});
				}
				if (globalState.avatars[userID-1]) {
					globalState.avatars[userID-1].visible = true ;
				}
				if (globalState.rightControls[userID-1]) {
					globalState.rightControls[userID-1].visible = true ;
				}
				if (globalState.leftControls[userID-1]) {
					globalState.leftControls[userID-1].visible = true ;
				}
				if (globalState.raycastLines[userID]) {
					globalState.raycastLines[userID].forEach(mesh => {
							globalState.scene.add(mesh);
					});
				}
				if (globalState.lineDrawing[userID]) {
					globalState.lineDrawing[0].forEach(filename => {
						const existingObject = globalState.scene.getObjectByName(filename);
						if (existingObject) {
							existingObject.visible = true ;
						  }
					});
				}

			}
			else {

				if (globalState.currentLineSegments[userID]) {

					globalState.scene.remove(globalState.currentLineSegments[userID]);

				}
				if (globalState.triangleMesh[userID]) {
					globalState.triangleMesh[userID].forEach(mesh => {
							globalState.scene.remove(mesh);
					});
				}

				if (globalState.avatars[userID-1]) {
					globalState.avatars[userID-1].visible = false ;
				}
				if (globalState.rightControls[userID-1]) {
					globalState.rightControls[userID-1].visible = false ;
				}
				if (globalState.leftControls[userID-1]) {
					globalState.leftControls[userID-1].visible = false ;
				}
				if (globalState.raycastLines[userID]) {
					globalState.raycastLines[userID].forEach(mesh => {
							globalState.scene.remove(mesh);
					});
				}
				if (globalState.lineDrawing[userID]) {
					globalState.lineDrawing[0].forEach(filename => {
						const existingObject = globalState.scene.getObjectByName(filename);
						if (existingObject) {
							existingObject.visible = false ;
						  }
						globalState.scene.remove(existingObject);
					});
				}
			}

			initializeOrUpdateSpeechBox();
			plotUserSpecificBarChart();
			plotUserSpecificDurationBarChart();

			// plotHeatmap();
			updatePointCloudBasedOnSelections();
			updateObjectsBasedOnSelections();
            updateMarkersBasedOnSelections();
		});
	}


};
function updateVisualization(nextTimestamp) {
    // Update elements based on the current timestamp
    for (let i = 0; i < numUsers; i++) {
        updateUserDevice(i, nextTimestamp);
        updateLeftControl(i, nextTimestamp);
        updateRightControl(i, nextTimestamp);
    }
}

function updateUserDevice(userId, timestamp = null) {
    const userField = `User${userId + 1}`; // Adjusting userId to match "User1" for index 0
    let deviceType = '';

    // Determine the device type based on the log mode
    if (logMode.vrGame || logMode.immersiveAnalytics) {
        deviceType = 'XRHMD';
    } else if (logMode.infoVisCollab || logMode.infoVisCollab1 || logMode.sceneNavigation || logMode.maintenance || logMode.videoScene) {
        deviceType = 'HandheldARInputDevice';
    } else {
        console.warn('Unsupported log mode.');
        return;
    }

    // Filter actions based on device type and user field
    const navigateActions = globalState.finalData.filter(action =>
        // action.Name === 'Navigate' &&
        action.TriggerSource === deviceType &&
        action.User === userField
    );

    const allSubActions = [];

    // If animating, filter actions by a specific timestamp
    if (globalState.isAnimating && timestamp !== null) {
        navigateActions.forEach(action => {
            action.Data.forEach(subAction => {
                const invokeTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
                if (invokeTime === timestamp) {  // Match the subAction timestamp with the provided timestamp
                    allSubActions.push({
                        parentAction: action,
                        ...subAction,
                        Timestamp: invokeTime
                    });
                }
            });
        });
    } else {
        // Otherwise, filter actions within a range of timestamps
        navigateActions.forEach(action => {
            action.Data.forEach(subAction => {
                const invokeTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
                if (invokeTime >= globalState.lineTimeStamp1 && invokeTime <= globalState.lineTimeStamp2) {
                    allSubActions.push({
                        parentAction: action,
                        ...subAction,
                        Timestamp: invokeTime
                    });
                }
            });
        });
    }

    // Sort subActions by Timestamp to process them in chronological order
    allSubActions.sort((a, b) => a.Timestamp - b.Timestamp);

    // Update avatars based on subActions
    allSubActions.forEach(subAction => {
        const location = parseLocation(subAction.ActionInvokeLocation);
        if (globalState.avatars[userId]) {
            globalState.avatars[userId].position.set(location.x, location.y, location.z);
            const euler = new THREE.Euler(
                THREE.MathUtils.degToRad(location.pitch),
                THREE.MathUtils.degToRad(location.yaw),
                THREE.MathUtils.degToRad(location.roll),
                'XYZ'
            );
            globalState.avatars[userId].rotation.set(0,0,0);
            globalState.avatars[userId].setRotationFromEuler(euler);
        }
    });

    // if (allSubActions.length === 0) {
    //     console.log('No suitable navigation actions found for user', userField, 'within the time range or at the specified timestamp.');
    // }
}


function updateLeftControl(userId, timestamp = null) {
    const userField = `User${userId + 1}`; // Adjusting userId to match "User1" for index 0
    let actionName, deviceType;

    // Determine actionName and deviceType based on the log mode
    if (logMode.immersiveAnalytics) {
        actionName = 'Move Hand';
        deviceType = 'XRHand_L';
    } else if (logMode.vrGame) {
        actionName = 'Move Controller';
        deviceType = 'XRController_L';
    } else {
        return; // For other log modes, do nothing
    }

    const navigateActions = globalState.finalData.filter(action =>
        // action.Name === actionName &&
        action.TriggerSource === deviceType &&
        action.User === userField
    );

    const allSubActions = [];

    // If animating, filter actions by a specific timestamp
    if (globalState.isAnimating && timestamp !== null) {
        navigateActions.forEach(action => {
            action.Data.forEach(subAction => {
                const invokeTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
                if (invokeTime === timestamp) {  // Match the subAction timestamp with the provided timestamp
                    allSubActions.push({
                        parentAction: action,
                        ...subAction,
                        Timestamp: invokeTime
                    });
                }
            });
        });
    } else {
        // Otherwise, filter actions within a range of timestamps
        navigateActions.forEach(action => {
            action.Data.forEach(subAction => {
                const invokeTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
                if (invokeTime >= globalState.lineTimeStamp1 && invokeTime <= globalState.lineTimeStamp2) {
                    allSubActions.push({
                        parentAction: action,
                        ...subAction,
                        Timestamp: invokeTime // Converted to milliseconds
                    });
                }
            });
        });
    }

    // Sort subActions by Timestamp to process them in chronological order
    allSubActions.sort((a, b) => a.Timestamp - b.Timestamp);

    // Update left controls based on subActions
    allSubActions.forEach(subAction => {
        const location = parseLocation(subAction.ActionInvokeLocation);
        if (globalState.leftControls[userId]) {
            globalState.leftControls[userId].position.set(location.x, location.y, location.z);
            const euler = new THREE.Euler(
                THREE.MathUtils.degToRad(location.pitch),
                THREE.MathUtils.degToRad(location.yaw),
                THREE.MathUtils.degToRad(location.roll),
                'XYZ'
            );
            globalState.leftControls[userId].rotation.set(0,0,0);

            // globalState.rightControls[userId].rotation.set(90,0,0);
            globalState.leftControls[userId].setRotationFromEuler(euler);
        }
    });
}

function updateRightControl(userId, timestamp = null) {
    const userField = `User${userId + 1}`; // Adjusting userId to match "User1" for index 0
    let actionName, deviceType;

    // Determine actionName and deviceType based on the log mode
    if (logMode.immersiveAnalytics) {
        actionName = 'Move Hand';
        deviceType = 'XRHand_R';
    } else if (logMode.vrGame) {
        actionName = 'Move Controller';
        deviceType = 'XRController_R';
    } else {
        return; // For other log modes, do nothing
    }

    const navigateActions = globalState.finalData.filter(action =>
        // action.Name === actionName &&
        action.TriggerSource === deviceType &&
        action.User === userField
    );

    const allSubActions = [];

    // If animating, filter actions by a specific timestamp
    if (globalState.isAnimating && timestamp !== null) {
        navigateActions.forEach(action => {
            action.Data.forEach(subAction => {
                const invokeTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
                if (invokeTime === timestamp) {  // Match the subAction timestamp with the provided timestamp
                    allSubActions.push({
                        parentAction: action,
                        ...subAction,
                        Timestamp: invokeTime
                    });
                }
            });
        });
    } else {
        // Otherwise, filter actions within a range of timestamps
        navigateActions.forEach(action => {
            action.Data.forEach(subAction => {
                const invokeTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
                if (invokeTime >= globalState.lineTimeStamp1 && invokeTime <= globalState.lineTimeStamp2) {
                    allSubActions.push({
                        parentAction: action,
                        ...subAction,
                        Timestamp: invokeTime // Converted to milliseconds
                    });
                }
            });
        });
    }

    // Sort subActions by Timestamp to process them in chronological order
    allSubActions.sort((a, b) => a.Timestamp - b.Timestamp);

    allSubActions.forEach(subAction => {
        const location = parseLocation(subAction.ActionInvokeLocation);
        if (globalState.rightControls[userId]) {
            globalState.rightControls[userId].position.set(location.x, location.y, location.z);
            const euler = new THREE.Euler(
                THREE.MathUtils.degToRad(location.pitch),
                THREE.MathUtils.degToRad(location.yaw),
                THREE.MathUtils.degToRad(location.roll),
                'XYZ'
            );
            globalState.rightControls[userId].rotation.set(0,0,0);

            globalState.rightControls[userId].setRotationFromEuler(euler);
        }
    });
}


function calculateDistance(point1, point2) {
    return Math.sqrt(
        Math.pow(point1.x - point2.x, 2) +
        Math.pow(point1.y - point2.y, 2) +
        Math.pow(point1.z - point2.z, 2)
    );
}

function generateDynamicColorMapping(uniqueUsers, uniqueActions) {
    const dynamicColorMapping = {};

    // Iterate through each unique user
    uniqueUsers.forEach(user => {
        dynamicColorMapping[user] = {};
        uniqueActions.forEach((action, index) => {
            const actionKey = `Action${index + 1}`;  // Generate keys like Action1, Action2, etc.
            if (userActionColors[user] && userActionColors[user][actionKey]) {
                dynamicColorMapping[user][action] = userActionColors[user][actionKey];  // Assign color from predefined set
            }
        });
    });

    return dynamicColorMapping;
}
// const dynamicColorMapping = generateDynamicColorMapping(uniqueUsers, uniqueActions);
function getColorForUserAction(userID, actionName) {
    return (dynamicColorMapping[userID] && dynamicColorMapping[userID][actionName]) || '#ffffff';
}

function updatePointCloudBasedOnSelections() {
    const data = globalState.finalData;
    const newFilteredActions = {};
	const hasVisibleUserID = Object.keys(globalState.show)
        .filter((userID) => globalState.show[userID])
        .map((userID) => `User${userID}`);
	const visibleContextUsers = hasVisibleUserID.filter(userId => {
		return userId in globalState.viewProps && globalState.viewProps[userId]["Context"] === true;
	});

	const nonVisibleContextUsers = hasVisibleUserID.filter(userId => {
		return userId in globalState.viewProps && globalState.viewProps[userId]["Context"] === false;
	});
	//remove loadedClouds for selected user not selected context
	nonVisibleContextUsers.forEach((nvcUser) => {
		if(nvcUser in globalState.loadedClouds){
			Object.keys(globalState.loadedClouds[nvcUser]).forEach(key => {
				const obj = globalState.scene.getObjectByName(key);
				console.log('Current objects in scene:', globalState.scene.children.map(child => child.name));

				if (obj) {
					globalState.scene.remove(obj);
					delete globalState.loadedClouds[nvcUser][key];
					console.log(`Removed object: ${key}`);
				}
			});
		}
	});

	visibleContextUsers.forEach((vcUser) => {
		newFilteredActions[vcUser] = new Set();
	});

	const filteredActions = data.filter(action => {
		const hasVisibleUserID = visibleContextUsers.some(userID => action.User.includes(userID));
        return hasVisibleUserID && action.Data.some(subAction => {
            const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
            const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration);

            if (actionEndTime >= globalState.lineTimeStamp1 && actionStartTime <= globalState.lineTimeStamp2 && subAction.ActionContext) {
                //const adjustedPath = `${globalState.objFilePath}${subAction.ActionContext}`;
                newFilteredActions[action.User].add(`${globalState.objFilePath}${subAction.ActionContext}`);
                return true;
            }
            return false;
        });
    });

    // Remove objects that are no longer relevant
    Object.keys(globalState.loadedClouds).forEach(userId => {
		Object.keys(globalState.loadedClouds[userId]).forEach(key => {
			const obj = globalState.scene.getObjectByName(key);
			if (obj && !newFilteredActions[userId].has(key)) {
				globalState.scene.remove(obj);
				delete globalState.loadedClouds[userId][key];
				// console.log(`Removed object: ${key}`);
			}
		});
    });
    if (hasVisibleUserID.length === 0 || visibleContextUsers.length === 0) {
		return;
	}

    // Load new and keep existing relevant objects
    for (const action of filteredActions) {
        for (const subAction of action.Data) {
            if (subAction.ActionContext !== null && globalState.loadedClouds?.[action.User]?.[subAction.ActionContext] === undefined
                && subAction.ActionInvokeLocation !== null )  {
				const adjustedPath = `${globalState.objFilePath}${subAction.ActionContext}`;

                if (globalState.loadedClouds[action.User] === undefined) {
                    globalState.loadedClouds[action.User] = {};
                }
                if (!globalState.loadedClouds[action.User].hasOwnProperty(adjustedPath))
               {

                globalState.loadedClouds[action.User][adjustedPath] = loadAvatarModel(adjustedPath)
                .then(obj => {
                    obj.name = adjustedPath;
                    globalState.scene.add(obj);
                    return obj;
                })
                .catch(error => {
                    console.error(`Failed to load object` +  error);
                    delete globalState.loadedClouds[action.User][adjustedPath];
                });
            }
            }
        }
    }
}

async function updateMarkersBasedOnSelections() {
    const data = globalState.finalData;  // The source data containing all actions
    const newFilteredActions = {};  // Object to keep track of filtered actions for each user
    const selectedActions = getSelectedTopics();  // Get the list of selected topics or actions

    // Determine which users have objects to display
    const selectedUsers = Object.keys(globalState.show)
        .filter(userID => globalState.show[userID])
        .map(userID => `User${userID}`);

    const visibleObjectUsers = selectedUsers.filter(userId => {
        return userId in globalState.viewProps && globalState.viewProps[userId]["Tracemap"] === true;
    });

    // Initialize filtered actions for visible users
    visibleObjectUsers.forEach((vcUser) => {
        newFilteredActions[vcUser] = [];
    });

    // Filter actions based on time range and other criteria
    for (const action of data) {
        for (const subAction of action.Data) {
            const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
            const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration); // Use `action.Duration` instead of `subAction.Duration`

            if (
                actionEndTime >= globalState.lineTimeStamp1 &&
                actionStartTime <= globalState.lineTimeStamp2 &&
                selectedActions.includes(action.Name) &&
                visibleObjectUsers.includes(action.User)
            ) {
                // Add action and subAction details to the filtered actions
                newFilteredActions[action.User].push({ subAction, action });
            }
        }
    }

    // Unload markers that are no longer needed
    for (const userID of Object.keys(globalState.markers || {})) {
        for (const key of Object.keys(globalState.markers[userID] || {})) {
            const markerExists = newFilteredActions[userID]?.some(filteredAction => {
                const { subAction } = filteredAction;
                const keyForCheck = `${subAction.ActionInvokeTimestamp}_${subAction.ActionReferentLocation}`;
                return keyForCheck === key;
            });

            if (!markerExists) {
                const markerGroup = globalState.markers[userID][key];  // This is a THREE.Group containing the shape and border

                if (markerGroup) {
                    // Remove the marker group from the scene
                    globalState.scene.remove(markerGroup);

                    // Dispose of each child in the group (shape mesh and border mesh)
                    markerGroup.children.forEach(child => {
                        if (child.geometry) {
                            child.geometry.dispose();  // Dispose of the geometry
                        }
                        if (child.material) {
                            child.material.dispose();  // Dispose of the material
                        }
                    });

                    // Clear the group from memory
                    markerGroup.clear();

                    // Remove reference from global state
                    delete globalState.markers[userID][key];
                }
            }
        }
    }

    // Place new markers for filtered actions
    for (const userID of Object.keys(newFilteredActions)) {
        newFilteredActions[userID].forEach(({ subAction, action }) => {
            const key = `${subAction.ActionInvokeTimestamp}_${subAction.ActionInvokeLocation}`;

            if (!globalState.markers) globalState.markers = {};
            if (!globalState.markers[userID]) globalState.markers[userID] = {};

            // Ensure the marker is not already created
            if (!globalState.markers[userID][key]) {
                const location = parseLocation(subAction.ActionInvokeLocation);  // Use the actual data to find location

                let tooClose = false;
                if (action.Name === "Navigate") {
                    for (const existingMarkerKey of Object.keys(globalState.markers[userID])) {
                        const existingMarker = globalState.markers[userID][existingMarkerKey];
                        const existingPosition = existingMarker.position;
                        const distance = calculateDistance(location, existingPosition);

                        if (distance < 0.1) {
                            tooClose = true;  // Skip adding this marker
                            console.log("Skipped marker due to proximity");
                            break;
                        }
                    }
                }

                if (!tooClose) {  // Only add the marker if it's not too close to existing markers (or not "Navigate")
                    // Create a sphere marker
                    const actionIndex = uniqueActions.indexOf(action.Name);
                    // const marker = createSphereMarker(actionIndex);  // Create a sphere marker with color
                    const marker = createSphereMarker(userID, action.Name);
                    marker.position.set(location.x, location.y, location.z);
                    marker.name = `Marker_${key}`;
                    globalState.scene.add(marker);  // Add marker to the scene

                    // Store marker in globalState for later management
                    globalState.markers[userID][key] = marker;
                }
            }
        });
    }
}

async function updateObjectsBasedOnSelections() {
    const data = globalState.finalData;
    const newFilteredActions = {};
    const actionsToLoad = {};
    const selectedActions = getSelectedTopics();

    // Gather all actions that meet the time range and have not been loaded yet
    const selectedUsers = Object.keys(globalState.show)
        .filter(userID => globalState.show[userID])
        .map(userID => `User${userID}`);

    const visibleObjectUsers = selectedUsers.filter(userId => {
        return userId in globalState.viewProps && globalState.viewProps[userId]["Object"] === true;
    });

    const nonVisibleObjectUsers = selectedUsers.filter(userId => {
        return userId in globalState.viewProps && globalState.viewProps[userId]["Object"] === false;
    });

    // Remove loadedObjects for selected users who are not selecting objects
    for (const nvoUser of nonVisibleObjectUsers) {
        if (nvoUser in globalState.loadedObjects) {
            for (const key of Object.keys(globalState.loadedObjects[nvoUser])) {
                if (globalState.loadedObjects[nvoUser][key]) {
                    try {
                        const obj = await globalState.loadedObjects[nvoUser][key]; // Ensure the object is fully loaded

                        // Iteratively remove all instances of the object from the scene
                        while (obj && obj.parent) { // Check if the object is still part of the scene
                            if (obj.geometry) obj.geometry.dispose(); // Dispose resources
                            if (obj.material) obj.material.dispose();
                            globalState.scene.remove(obj);
                        }

                        // Once all instances are removed, delete from loaded objects
                        delete globalState.loadedObjects[nvoUser][key];
                    } catch (error) {
                        console.error(`Error removing object ${key}:`, error);
                    }
                }
            }
        }
    }

    visibleObjectUsers.forEach((vcUser) => {
        newFilteredActions[vcUser] = new Set();
        actionsToLoad[vcUser] = [];
    });

    for (const action of data) {
        if (action.Data.length === 0) continue; // Skip if no subactions

        let originLocation = null;
        if (action.Data.length > 1 && action.ReferentType == "Virtual") {
            const firstDataEntry = action.Data[0];
            if (firstDataEntry.ActionReferentLocation)
            {
                originLocation = parseLocation(firstDataEntry.ActionReferentLocation);
            }
        }

        for (const subAction of action.Data) {
            const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
            const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration);

            if (actionEndTime >= globalState.lineTimeStamp1 && actionStartTime <= globalState.lineTimeStamp2 &&
                subAction.ActionReferentBody && action.ReferentType === "Virtual" &&
                selectedActions.includes(action.Name) && visibleObjectUsers.includes(action.User)) {

                const key = `${subAction.ActionInvokeTimestamp}_${subAction.ActionReferentBody}`;

                if (!newFilteredActions[action.User].has(key)) {
                    newFilteredActions[action.User].add(key);
                    actionsToLoad[action.User].push({ key, subAction, originLocation }); // Add originLocation for delta calculation
                }
            }
        }
    }

    // Unload objects that are no longer needed
    for (const userID of Object.keys(globalState.loadedObjects)) {
        for (const key of Object.keys(globalState.loadedObjects[userID])) {
            if (!newFilteredActions[userID].has(key) && globalState.loadedObjects[userID][key]) {
                const obj = await globalState.loadedObjects[userID][key];  // Ensure the object is fully loaded
                if (obj && obj.parent) { // Check if the object is still part of the scene
                    if (obj.geometry) obj.geometry.dispose(); // Dispose resources
                    if (obj.material) obj.material.dispose();
                    globalState.scene.remove(obj);
                    delete globalState.loadedObjects[userID][key];
                }
            }
        }
    }

    if (selectedUsers.length === 0 || visibleObjectUsers.length === 0) {
        return;
    }

    // Load new objects that are required
    for (const userID of Object.keys(actionsToLoad)) {
        for (const { key, subAction, originLocation } of actionsToLoad[userID]) {
            if (globalState.loadedObjects[userID] === undefined) {
                globalState.loadedObjects[userID] = {};
            }
            if (!globalState.loadedObjects?.[userID]?.[key]) { // Double check to prevent race conditions
                if (!globalState.loadedObjects[userID].hasOwnProperty(key)) {
                    const adjustedPath = `${globalState.objFilePath}${subAction.ActionReferentBody}`;
                    globalState.loadedObjects[userID][key] = loadAvatarModel(adjustedPath)
                        .then(obj => {
                            obj.name = key;
                            const location = parseLocation(subAction.ActionReferentLocation);
                            if (logMode.videoScene)
                            {
                                obj.position.y += 0.25; 
                            }

                            // && video === true
                            if (logMode.vrGame && originLocation) {  // Only adjust if originLocation exists
                                // Calculate the delta from the origin
                                // console.log("came here with object " + adjustedPath);
                                const existingObject = globalState.scene.getObjectByName(key);

                                const deltaX = location.x - originLocation.x;
                                const deltaY = location.y - originLocation.y;
                                const deltaZ = location.z - originLocation.z;
                                console.log(existingObject);
                                if (existingObject) {
                                    console.log("ur changing it here !");
                                    existingObject.position.set(deltaX, deltaY, deltaZ);
                                }
                                // obj.position.set(deltaX, deltaY, deltaZ);
                            }

                            return obj; // Return the loaded object
                        })
                        .catch(error => {
                            console.error(`Failed to load object ${key}:`, error);
                            delete globalState.loadedObjects[userID][key]; // Clean up state on failure
                        });
                }
            }
        }
    }
}

function createSphereMarker(userID, actionName) {
    const radius = 0.03;  // Reduced size for better visualization
    const widthSegments = 32;
    const heightSegments = 32;

    // Get color based on user ID and action name
    const color = getColorForUserAction(userID, actionName);

    // Create sphere geometry
    const sphereGeometry = new THREE.SphereGeometry(radius, widthSegments, heightSegments);

    // Material with color based on user ID and action
    const markerMaterial = new THREE.MeshBasicMaterial({
        color: color,  // Use color from dynamicColorMapping
        transparent: false,
        opacity: 0.8  // Set opacity to 80%
    });

    const sphereMesh = new THREE.Mesh(sphereGeometry, markerMaterial);

    return sphereMesh;
}

//user choice to use heatmap or tracemap
function plotHeatmap() {
    return ;

    const hasVisibleUserID = Object.keys(globalState.show)
        .filter((userID) => globalState.show[userID])
        .map((userID) => `User${userID}`);

	const visibleHeatmapUsers = hasVisibleUserID.filter(userId => {
		return userId in globalState.viewProps && globalState.viewProps[userId]["Heatmap"] === true;
	});

	// const nonVisibleHeatmapUsers = hasVisibleUserID.filter(userId => {
	// 	return globalState.viewProps[userId]["Heatmap"] === false;
	// });

	Object.keys(globalState.heatmaps).forEach(userId => {
		const userHeatmap = globalState.heatmaps[userId];
		if (userHeatmap) {
            userHeatmap.forEach(heatmap => {
                if (heatmap.mesh){
                    globalState.scene.remove(heatmap.mesh);
                }
            });
		}
	});

	if (hasVisibleUserID.length === 0 || visibleHeatmapUsers.length === 0) {
		return;
	}

	globalState.heatmaps = {}
	visibleHeatmapUsers.forEach((vhi) => {
		globalState.heatmaps[vhi] = [];
	})

    const selectedActions = getSelectedTopics();
    const data = globalState.finalData;
    const gridSize = 50; // Adjust the size of the grid
    const voxelSize = 0.1; // was 0.1 earlier

    const gridHelper = new THREE.GridHelper(gridSize * voxelSize, gridSize, 0x888888, 0x444444);
    gridHelper.position.set((gridSize * voxelSize) / 2, 0, (gridSize * voxelSize) / 2);

    visibleHeatmapUsers.forEach((user, userIndex) => {
        // Create a 3D voxel grid for this user's heatmap
        const heatmap = new Array(gridSize)
            .fill()
            .map(() =>
                new Array(gridSize)
                    .fill()
                    .map(() => new Array(gridSize).fill(0))
            );

        // Use selected actions and time range to filter actions for the user
        const actionsToDisplay = data.filter((action) => {
            return (
                action.User === user &&
                selectedActions.includes(action.Name) &&
                action.Data.some((subAction) => {
                    const actionStartTime = parseTimeToMillis(
                        subAction.ActionInvokeTimestamp
                    );
                    const actionEndTime =
                        actionStartTime + parseDurationToMillis(action.Duration);
                    return (
                        actionEndTime >= globalState.lineTimeStamp1 &&
                        actionStartTime <= globalState.lineTimeStamp2
                    );
                })
            );
        });

        actionsToDisplay.forEach((action) => {
            action.Data.forEach((subAction) => {
                const location = parseLocation(subAction.ActionInvokeLocation);
                if (location) {
                    const gx = Math.floor(location.x / voxelSize);
                    const gy = Math.floor(location.y / voxelSize);
                    const gz = Math.floor(location.z / voxelSize);
                    if (
                        gx >= 0 &&
                        gx < gridSize &&
                        gy >= 0 &&
                        gy < gridSize &&
                        gz >= 0 &&
                        gz < gridSize
                    ) {
                        heatmap[gx][gy][gz] += 1;
                    }
                }
            });
        });
        const smoothedHeatmap = applyGaussianBlur3D(heatmap);
        renderHeatmap(heatmap, user, voxelSize);
        // console.log("here?");
    });
}

function renderHeatmap(heatmap, user, voxelSize) {
    const group = new THREE.Group();
    for (let x = 0; x < heatmap.length; x++) {
        for (let y = 0; y < heatmap[x].length; y++) {
            for (let z = 0; z < heatmap[x][y].length; z++) {
                const intensity = heatmap[x][y][z];
                if (intensity > 0) {
                    const userColor = colorScale(user); // Use the user-specific color from the scale
                    const color = new THREE.Color(userColor); // Map intensity to color
                    const material = new THREE.MeshBasicMaterial({
                        color,
                        transparent: true,
                        opacity: Math.min(1, intensity / 5),
                        // opacity: Math.min(1, intensity / 20),
                    });
                    // const cubeSizeFactor = 0.5; // Change this value to control the size reduction
                    // const cube = new THREE.Mesh(
                    //     new THREE.BoxGeometry(voxelSize * cubeSizeFactor, voxelSize * cubeSizeFactor, voxelSize * cubeSizeFactor),
                    //     material
                    // );
                    // const cube = new THREE.Mesh(
                    //     new THREE.PlaneGeometry(voxelSize, voxelSize), // 2D square for visualization
                    //     material
                    // );
                    const cube = new THREE.Mesh(
                        new THREE.SphereGeometry(voxelSize / 2),
                        material
                    );
                    // const cube = new THREE.Mesh(
                    //     new THREE.CylinderGeometry(voxelSize / 2, voxelSize / 2, voxelSize/2, 6), // Hexagonal prism
                    //     material
                    // );
                    cube.position.set(
                        x * voxelSize,
                        y * voxelSize,
                        z * voxelSize
                    );
                    group.add(cube);
                }
            }
        }
    }
    globalState.scene.add(group); // Add the heatmap to the scene
    globalState.heatmaps[user].push({
        mesh: group,
    });
}

function applyGaussianBlur3D(heatmap) {
    const kernelSize = 5;
    const sigma = 2.0;
    const kernel = createGaussianKernel(kernelSize, sigma);
    const smoothedHeatmap = JSON.parse(JSON.stringify(heatmap));
    for (let x = 0; x < heatmap.length; x++) {
        for (let y = 0; y < heatmap[x].length; y++) {
            for (let z = 0; z < heatmap[x][y].length; z++) {
                let sum = 0;
                let weightSum = 0;
                for (let i = -1; i <= 1; i++) {
                    for (let j = -1; j <= 1; j++) {
                        for (let k = -1; k <= 1; k++) {
                            const nx = x + i;
                            const ny = y + j;
                            const nz = z + k;
                            if (
                                nx >= 0 &&
                                nx < heatmap.length &&
                                ny >= 0 &&
                                ny < heatmap[x].length &&
                                nz >= 0 &&
                                nz < heatmap[x][y].length
                            ) {
                                const weight = kernel[i + 1][j + 1][k + 1];
                                sum += heatmap[nx][ny][nz] * weight;
                                weightSum += weight;
                            }
                        }
                    }
                }
                smoothedHeatmap[x][y][z] = sum / weightSum;
            }
        }
    }
    return smoothedHeatmap;
}

function createGaussianKernel(size, sigma) {
    const kernel = new Array(size).fill().map(() => new Array(size).fill().map(() => new Array(size).fill(0)));
    const mean = Math.floor(size / 2);
    let sum = 0.0;
    for (let x = 0; x < size; x++) {
        for (let y = 0; y < size; y++) {
            for (let z = 0; z < size; z++) {
                const exponent = -(
                    ((x - mean) ** 2 + (y - mean) ** 2 + (z - mean) ** 2) /
                    (2 * sigma ** 2)
                );
                kernel[x][y][z] = Math.exp(exponent);
                sum += kernel[x][y][z];
            }
        }
    }
    // Normalize the kernel
    for (let x = 0; x < size; x++) {
        for (let y = 0; y < size; y++) {
            for (let z = 0; z < size; z++) {
                kernel[x][y][z] /= sum;
            }
        }
    }
    return kernel;
}

function parseLocation(locationString) {
    const parts = locationString.split(',');
    if (parts.length !== 6) {
        console.error('Invalid location format:', locationString);
        return null;
    }
    return {
        x: -parseFloat(parts[0]), //unity -> three.js
        y: parseFloat(parts[1]),
        z: parseFloat(parts[2]),
        pitch: parseFloat(parts[3]),  // Rotation around X-axis in degrees
        yaw: -parseFloat(parts[4]),    // Rotation around Y-axis in degrees
        roll: parseFloat(parts[5])    // Rotation around Z-axis in degrees
    };
}


async function initializeScene() {
	globalState.scene = new THREE.Scene();
	globalState.scene.background = new THREE.Color(0xF5F5F5);
	const spatialView = document.getElementById('spatial-view');
	globalState.camera = new THREE.PerspectiveCamera(40, spatialView.innerWidth / spatialView.innerHeight, 0.1, 1000);
	globalState.camera.position.set(1, 3, 7);
	// globalState.camera.updateProjectionMatrix();

	globalState.renderer = new THREE.WebGLRenderer({
	  antialias: true
	});


	globalState.renderer.setSize(spatialView.width, spatialView.height);
	globalState.renderer.toneMapping = THREE.LinearToneMapping;
	// globalState.renderer.toneMappingExposure = 0.01;

	document.getElementById('spatial-view').appendChild(globalState.renderer.domElement);


	globalState.controls = new OrbitControls(globalState.camera, globalState.renderer.domElement);
	globalState.controls.enableZoom = true;

	const ambientLight = new THREE.AmbientLight(0xffffff, 1.0);
	globalState.scene.add(ambientLight);
	const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
	directionalLight.position.set(0, 1, 0);
	globalState.scene.add(directionalLight);

	globalState.controls.update();

	const gridHelper = new THREE.GridHelper(10, 10);
	gridHelper.position.y = -1;
	// globalState.scene.add(gridHelper);
	// await Promise.all([loadRoomModel()]);

  	const finalData = await Promise.all([
		fetch(globalState.logFIlePath).then(response => response.json()),
		  ]);
  globalState.finalData = finalData[0];

    updateNumUsers();
    initializeViewProps();
    window_onload();
    const isHeadsetMode = logMode.vrGame || logMode.immersiveAnalytics;
    const avatarModel = isHeadsetMode ? 'headset.glb' : 'ipad.glb';
    const loadModel = isHeadsetMode ? loadAvatarModel : loadIpadModel;
    const avatarPromises = Array.from({ length: numUsers }, () => loadModel(avatarModel));


	globalState.avatars = await Promise.all(avatarPromises);
	const controlModels = {
		vrGame: { right: "controller_r.glb", left: "controller_l.glb" },
		immersiveAnalytics: { right: "hand_r.glb", left: "hand_l.glb" },
	};

	// Determine right and left control models
	let rightControlModel, leftControlModel;

	if (logMode.vrGame) {
		({ right: rightControlModel, left: leftControlModel } = controlModels.vrGame);
	} else if (logMode.immersiveAnalytics) {
		({ right: rightControlModel, left: leftControlModel } = controlModels.immersiveAnalytics);
	} else {
		// Other modes do not use controls
		globalState.rightControls = [];
		globalState.leftControls = [];
	}

	// Load right and left controls for each user if models are defined
	if (rightControlModel && leftControlModel) {
		const rightControlPromises = Array.from({ length: numUsers }, () => loadHand(rightControlModel));
		const leftControlPromises = Array.from({ length: numUsers }, () => loadHand(leftControlModel));

		globalState.rightControls = await Promise.all(rightControlPromises);
		globalState.leftControls = await Promise.all(leftControlPromises);
	}

	setTimes(globalState.finalData);

}

function createPlotTemporal() {

	const topicsData = globalState.finalData.map(action => {
        if (action.Data.length > 0) {
            const firstEvent = action.Data[0]; // Always using the first event
            const startTimeMillis = parseTimeToMillis(firstEvent.ActionInvokeTimestamp);
            const endTimeMillis = startTimeMillis + parseDurationToMillis(action.Duration);
            return {
                topic: action.Name, // Using 'Name' to identify the type of action
                startTime: startTimeMillis,
                endTime: endTimeMillis,
                isUserInterest: false, // Placeholder for now
                hasUserInterestAction: false // Placeholder for now
            };
        }
    }).filter(action => action !== undefined && action.startTime && action.endTime);


    const temporalViewContainer = d3.select("#temporal-view");
    const width = (document.getElementById('spatial-view').clientWidth - margin.left - margin.right) * 0.9;
	const height = 310; //adjusted for 1080p or above
    const speechPlotSvg = d3.select("#speech-plot-container");
	speechPlotSvg.html("");
	const svg = speechPlotSvg.append('svg')
        .attr("width", globalState.dynamicWidth + margin.left + margin.right)
        .attr('height', margin.top + margin.bottom + height)
        .append('g')
        .attr('transform', `translate(${margin.left}, ${margin.top})`);

    // X scale for time
    const x = d3.scaleTime()
        .domain([d3.min(topicsData, d => d.startTime), d3.max(topicsData, d => d.endTime)])
        .range([0, globalState.dynamicWidth]);

    // Y scale for user actions
    const yScale = d3.scaleBand()
        .domain(topicsData.map(d => d.topic))
        .rangeRound([0, height])
        .padding(0.1);

    // Append the Y-axis
	const yAxis = svg.append("g")
	.attr("class", "axis axis--y")
	.call(d3.axisLeft(yScale));

	yAxis.selectAll("text")
	.style("font-size", "14px"); // Adjust the font size as needed

	// Calculate density
    const densityData = topicsData.map(d => {
        const totalDuration = d.endTime - d.startTime;
        const density = totalDuration / (globalState.lineTimeStamp2 - globalState.lineTimeStamp1); // Simplified density calculation
        return { ...d, density };
    });

    // Create color scale for density
    const colorScale = d3.scaleSequential(d3.interpolateBlues) //interpolateBlues //interpolateOranges //interpolatePurples //interpolateGreys //interpolateViridis //interpolateCividis
        .domain([0, d3.max(densityData, d => d.density*0.6)]);

    // Drawing bars for each action with density-based color
    svg.selectAll(".bar")
        .data(densityData)
        .enter().append("rect")
        .attr("class", "bar")
        .attr("x", d => x(d.startTime))
        .attr("y", d => yScale(d.topic) + (yScale.bandwidth() * 0.15)) // Center the bar in the y band
        .attr("width", d => x(d.endTime) - x(d.startTime))
        .attr("height", yScale.bandwidth() * 0.7)
        .attr("fill", d => colorScale(d.density)); // Apply density color

}

function drawBookmarks(llmTS) {
    const svg = d3.select(`#shared-axis-container svg`); // Target the correct SVG by container ID
	svg.selectAll(".bookmark-marker").remove(); // Clear existing bookmarks

	const xScale = d3.scaleTime()
		.domain([new Date(globalState.globalStartTime), new Date(globalState.globalEndTime)])
		.range([0, globalState.dynamicWidth]);

	// Updated bookmark path with appropriate size
	// const bookmarkPath = "M15 3 L15 25.5 L7.5 15 L0 25.5 L0 3 Z";
    const bookmarkPath = "M22.5 4.5 L22.5 38.25 L11.25 22.5 L0 38.25 L0 4.5 Z";

    Object.entries(llmTS).forEach(([id, times]) => {
        times.forEach(timeStr => {
            const timestampMs = parseTimeToMillis(timeStr);
            const xPosition = xScale(timestampMs); // Use xScale to find the position

            if (timestampMs >= new Date(globalState.globalStartTime).getTime()) {
				const bookmarkGroup = svg.append("g") // Group to keep path and text together
                    .attr("transform", `translate(${xPosition + margin.left + margin.right}, 10)`)
                    .attr("class", "bookmark-marker")
                    .attr("id", `bookmark-${id}`)
                    .on("mouseover", function() {
                        d3.select(this).select("path")
                            .attr("fill", "#ff5722");
                    })
                    .on("mouseout", function() {
                        d3.select(this).select("path")
                            .attr("fill", "#ff9800");
                    })
                    .on("click", function() {
                        console.log(`Focusing on entry with ID: ${id}, ${new Date(timestampMs)}`);
						highlightAndScrollToInsight(id);
                    });

                // Append bookmark path
                bookmarkGroup.append("path")
                    .attr("d", bookmarkPath)
                    .attr("fill", "#ff9800");

                    bookmarkGroup.append("text")
                    .attr("x", 11.25)  // Centered horizontally in the bookmark (half of 22.5)
                    .attr("y", 20)     // Vertically aligned in the bookmark (adjusted for the new height)
                    .attr("text-anchor", "middle")
                    .attr("fill", "#000") // Black color for contrast
                    .attr("font-size", "14px") // Adjust the font size to fit inside the bookmark
                    .attr("font-weight", "bold")
                    .text(id); // Use the key as the text
                }
        });
    });
}

// Function to update the zoom level display
function updateZoomLevelDisplay(zoomLevel) {
    const zoomDisplay = document.getElementById('zoom-level-display');
    zoomDisplay.textContent = `Zoom: ${zoomLevel}%`;
}

// Example scroll/zoom event listener
let currentZoomLevel = 100; // Assuming 100% is the default zoom level
document.getElementById('spatial-view').addEventListener('wheel', function(event) {
    
    // Assuming that scrolling up zooms in and scrolling down zooms out
    if (event.deltaY < 0) {
        currentZoomLevel = Math.min(currentZoomLevel + 10, 200); // Max zoom 200%
    } else {
        currentZoomLevel = Math.max(currentZoomLevel - 10, 10); // Min zoom 10%
    }

    updateZoomLevelDisplay(currentZoomLevel);

    // Prevent default scrolling behavior
    event.preventDefault();
});

function setTimes(data) {
	// Assuming data is an array of action records
	let timestamps = [];

	data.forEach(action => {
	  if (action.Data && Array.isArray(action.Data)) {
		action.Data.forEach(subAction => {
		  if (subAction.ActionInvokeTimestamp) {
			timestamps.push(parseTimeToMillis(subAction.ActionInvokeTimestamp));
		  }
		});
	  }
	});
	timestamps.sort((a, b) => a - b);

	const globalStartTime = timestamps[0];
	const globalEndTime = timestamps[timestamps.length - 1];

	console.log("Global Start Time:", new Date(globalStartTime));
	console.log("Global End Time:", new Date(globalEndTime));

	globalState.globalStartTime = globalStartTime;
	globalState.globalEndTime = globalEndTime;
	const totalTime = globalEndTime - globalStartTime;
	globalState.intervalDuration = totalTime / globalState.bins;
	globalState.intervals = Array.from({
	  length: globalState.bins + 1
	}, (v, i) => new Date(globalStartTime + i * globalState.intervalDuration));
	globalState.lineTimeStamp1 = globalStartTime;
	globalState.lineTimeStamp2 = globalStartTime + 5000; // adding 5 second by default
}

export function getGlobalState() {
	return globalState;
}

function createLines(timestamp1, timestamp2) {
    const heightFactor = 1.1;
    const y1 = 55;
    const alignX = 10;
	const svg = d3.select("#temporal-view");
	let height = parseInt(d3.select("#speech-plot-container").style("height")) * heightFactor;
    const dynamicWidth = globalState.dynamicWidth;

    let xPosition1 = Math.max(0, x(new Date(timestamp1))) + margin.left + alignX;
    let xPosition2 = Math.max(0, x(new Date(timestamp2))) + margin.left + alignX;

	let circle1 = svg.select('#time-indicator-circle1');
	circle1.attr('class', 'interactive');
	if (circle1.empty()) {
		circle1 = svg.append('circle')
			.attr('id', 'time-indicator-circle1')
			.attr('r', 5)
			.style('fill', '#9e9e9e');
	}

	let circle2 = svg.select('#time-indicator-circle2');
	circle2.attr('class', 'interactive');
	if (circle2.empty()) {
		circle2 = svg.append('circle')
			.attr('id', 'time-indicator-circle2')
			.attr('r', 5)
			.style('fill', '#9e9e9e');
	}

	function dragstarted(event, d) {
		d3.select(this).raise().classed("active", true);
	}

	function dragended(event, d) {
		d3.select(this).classed("active", false);
		generateHierToolBar();
		createPlotTemporal();
		plotUserSpecificBarChart();
		plotUserSpecificDurationBarChart();
	}
	var drag = d3.drag()
	.on("start", dragstarted)
	.on("drag", dragged)
	.on("end", dragended);

	let line1 = svg.select('#time-indicator-line1');

	if (line1.empty()) {
		line1 = svg.append('line').attr('id', 'time-indicator-line1');
	}

	line1.attr('x1', xPosition1)
		.attr('x2', xPosition1)
		.attr('y1', y1)
		.attr('y2', height)
		.style('stroke', '#9e9e9e')
		.style('stroke-width', '3')
		.style('opacity', 1)
		.attr('class', 'interactive')
		.call(drag);

	circle1.attr('cx', xPosition1)
		.attr('cy', y1)
		.call(drag);

	let line2 = svg.select('#time-indicator-line2');

	if (line2.empty()) {
		line2 = svg.append('line').attr('id', 'time-indicator-line2');
	}

	line2.attr('x1', xPosition2)
		.attr('x2', xPosition2)
		.attr('y1', y1)
		.attr('y2', height)
		.style('stroke', '#9e9e9e')
		.style('stroke-width', '3')
		.style('opacity', 1)
		.attr('class', 'interactive')
		.call(drag);

	circle2.attr('cx', xPosition2)
		.attr('cy', y1)
		.call(drag);


	circle1.call(drag);
	circle2.call(drag);
	updateRangeDisplay(timestamp1,timestamp2);
	updateXRSnapshot();
	for (let i = 0; i < numUsers; i++) {
        updateUserDevice(i);
        updateLeftControl(i);
        updateRightControl(i);
    }
	plotHeatmap();
	updatePointCloudBasedOnSelections();
	updateObjectsBasedOnSelections();
    updateMarkersBasedOnSelections();
	initializeShadedAreaDrag();
}

export function dragged(event,d) {
	const svgElement = document.querySelector("#temporal-view svg");
	const container = document.getElementById("temporal-view");
	const indicatorSvg = document.getElementById('indicator-svg');
	indicatorSvg.style.overflow = "visible";

	const height = parseInt(d3.select(svgElement).style("height")) - margin.top - margin.bottom;

	let newXPosition = event.x - margin.left;
	let newTimestamp = x.invert(newXPosition);

	const id = d3.select(this).attr('id');
	const isLine1 = id === 'time-indicator-line1' || id === 'time-indicator-circle1';
	 const circleId = isLine1 ? '#time-indicator-circle1' : '#time-indicator-circle2';
	const otherCircleId = isLine1 ? '#time-indicator-circle2' : '#time-indicator-circle1';
	const lineId = isLine1 ? 'time-indicator-line1' : 'time-indicator-line2';
	const otherLineId = isLine1 ? 'time-indicator-line2' : 'time-indicator-line1';
	const timestampKey = isLine1 ? 'lineTimeStamp1' : 'lineTimeStamp2';
	let otherTimestamp = globalState[isLine1 ? 'lineTimeStamp2' : 'lineTimeStamp1'];
	const minDistanceMillis = 5000;

	if (newTimestamp > new Date(globalState.globalEndTime)) {
	  newTimestamp = new Date(globalState.globalEndTime);
	  newXPosition = x(newTimestamp);
	}
	if (isLine1) {
		newTimestamp = Math.min(newTimestamp, otherTimestamp - minDistanceMillis);
	} else {
		newTimestamp = Math.max(newTimestamp, otherTimestamp + minDistanceMillis);
	}
	if (isLine1) {
	  globalState.lineTimeStamp1 = newTimestamp;
	  globalState.lineTimeStamp2 = otherTimestamp;
	  globalState.currentTimestamp = newTimestamp - globalState.globalStartTime;
	  const binIndex = Math.floor(globalState.currentTimestamp / globalState.intervalDuration);
	  globalState.startTimeStamp = globalState.globalStartTime + (binIndex * globalState.intervalDuration);
	  globalState.endTimeStamp = globalState.startTimeStamp + globalState.intervalDuration;
	} else {
	  globalState.lineTimeStamp2 = newTimestamp;
	  globalState.lineTimeStamp1 = otherTimestamp;
	}
	newXPosition = x(new Date(newTimestamp));

	d3.select(this).attr('x1', newXPosition + margin.left).attr('x2', newXPosition + margin.left);
	d3.select(circleId).attr('cx', newXPosition + margin.left);


    createLines(globalState.lineTimeStamp1, globalState.lineTimeStamp2);
    const timeStamp1 = new Date(globalState.lineTimeStamp1);
    const timeStamp2 = globalState.lineTimeStamp2;
    updateRangeDisplay(timeStamp1, timeStamp2);
	updateXRSnapshot();

    initializeOrUpdateSpeechBox();

	// plotHeatmap();

	updatePointCloudBasedOnSelections();
	updateObjectsBasedOnSelections();
    updateMarkersBasedOnSelections();
	for (let i = 0; i < numUsers; i++) {

        // Update the user's device
        updateUserDevice(i);
        updateLeftControl(i);
        updateRightControl(i);
    }
    initializeShadedAreaDrag();
}

function updateNumUsers(){
	uniqueUsers = new Set(globalState.finalData.map(action => action.User));
	numUsers = uniqueUsers.size;
    uniqueActions = Array.from(new Set(globalState.finalData.map(action => action.Name)));
    dynamicColorMapping = generateDynamicColorMapping(uniqueUsers, uniqueActions);
    console.log(dynamicColorMapping);
	updateGlobalShow();
}

//Update global show, 0 index is not used
function updateGlobalShow(){
	globalState.show = Array(numUsers+1).fill(true);
}

function initHierToolBar(){
	const data = globalState.finalData;
	const uniqueActions = new Set();

	const toolbar = document.getElementById('hier-toolbar');
    toolbar.innerHTML = '';

    // Process each action directly, considering nested timestamps
    data.forEach(action => {
        action.Data.forEach(subAction => {
            uniqueActions.add(action.Name); // Use 'Name' to identify the type of action
        });
    });

    // Create toolbar items for each unique action name
    uniqueActions.forEach(actionName => {
        createTopicItem(actionName, toolbar);
    });
}

function enableCheckboxes(actionNames, shouldCheck = true) {
    // Get all checkboxes with the class 'topic-checkbox'
    const allCheckboxes = document.querySelectorAll('.topic-checkbox');

    allCheckboxes.forEach(checkbox => {
        const actionName = checkbox.value;
        if (actionNames.includes(actionName)) {
            checkbox.disabled = false; // Enable the checkbox
            checkbox.checked = shouldCheck; // Check or uncheck based on the parameter
        } else {
            checkbox.disabled = true; // Disable checkboxes not in the list
            checkbox.checked = true; // Optionally uncheck them as well
        }
    });
}

function generateHierToolBar() {
    const data = globalState.finalData;

    const uniqueActions = new Set();

    // Process each action directly, considering nested timestamps
    data.forEach(action => {
        action.Data.forEach(subAction => {
            const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
            const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration);
            if (actionEndTime >= globalState.lineTimeStamp1 && actionStartTime <= globalState.lineTimeStamp2) {
                uniqueActions.add(action.Name); // Use 'Name' to identify the type of action
            }
        });
    });

	enableCheckboxes([...uniqueActions], true); // enable and checked in the uniqueActions
}

// create the actual checkbox HTML elemets
function createTopicItem(actionName, toolbar,  isEnabled = false) {
    const topicItem = document.createElement('li');
    const topicCheckbox = document.createElement('input');
    topicCheckbox.type = 'checkbox';
    topicCheckbox.id = `checkbox_broadtopic_${actionName.replace(/\s+/g, '_')}`;
    topicCheckbox.className = 'topic-checkbox';
	topicCheckbox.value = actionName;

	// Set the checkbox enabled or disabled based on the `isEnabled` parameter
    topicCheckbox.disabled = !isEnabled;

    const label = document.createElement('label');
    label.htmlFor = topicCheckbox.id;
    label.textContent = actionName;
    label.style.color = '#000'; // Default color, adjust as needed

    topicItem.appendChild(topicCheckbox);
    topicItem.appendChild(label);
    toolbar.appendChild(topicItem);

    // Event listener for the checkbox
    topicCheckbox.addEventListener('change', function() {
		initializeOrUpdateSpeechBox();
		plotUserSpecificBarChart();
		plotUserSpecificDurationBarChart();

		// plotHeatmap();

		updatePointCloudBasedOnSelections();
		updateObjectsBasedOnSelections();
        updateMarkersBasedOnSelections();
    });

}

function generateUserLegends(){
	const legendContainer = document.getElementById('user-toolbar-container');
	legendContainer.style.height = `${numUsers * globalState.obContext.length *38}px`;

    for (let i = 1; i <= numUsers; i++) {
        // Create the main user checkbox
      const userDiv = document.createElement('div');
      userDiv.classList.add('checkbox-container', 'collapsed'); // Start as collapsed

      const userCheckboxLabel = document.createElement('label');
      userCheckboxLabel.classList.add('user-checkbox');

      const userCheckbox = document.createElement('input');
      userCheckbox.type = 'checkbox';
      userCheckbox.id = `toggle-user${i}`;
      userCheckbox.checked = true;
      userCheckboxLabel.appendChild(userCheckbox);

      const legendSquare = document.createElement('div');
      legendSquare.className = 'legend-square';
      legendSquare.style.backgroundColor = colorScale(`User${i}`);
      userCheckboxLabel.appendChild(legendSquare);

      userCheckboxLabel.appendChild(document.createTextNode(` User ${i}`));

      // Create the nested checkboxes container
      const nestedDiv = document.createElement('div');
      nestedDiv.classList.add('nested-checkboxes');

		globalState.obContext.forEach(context => {
			const contextLabel = document.createElement('label');
			const contextCheckbox = document.createElement('input');
			contextCheckbox.type = 'checkbox';
			contextCheckbox.id = `toggle-user${i}-${context}`;

			contextLabel.appendChild(contextCheckbox);
			contextLabel.appendChild(document.createTextNode(` ${context}`));
			nestedDiv.appendChild(contextLabel);
			nestedDiv.appendChild(document.createElement('br'));

			contextCheckbox.addEventListener('change', function () {
				handleContextChange(context, `User${i}`, this.checked);
			});
		});

		// Create horizontal dotted line
		const horizontalLine = document.createElement('div');
		horizontalLine.classList.add('horizontal-line');

		// Append elements
		userDiv.appendChild(userCheckboxLabel);
		userDiv.appendChild(nestedDiv);
		userDiv.appendChild(horizontalLine);
		legendContainer.appendChild(userDiv);

		if (userCheckbox.checked) {
			nestedDiv.classList.add('show');
			userDiv.classList.add('expanded'); // Set to expanded by default
		} else {
			userDiv.classList.add('collapsed');
		}

		
		userCheckbox.addEventListener('change', function () {
			// Get visible users by filtering the globalState.show
			const hasVisibleUserID = Object.keys(globalState.show)
			.filter((userID) => globalState.show[userID])
			.map((userID) => `User${userID}`);

		
		const baseHeightPerUser = 49; // Base height for each expanded user (adjust based on your UI)
		const obContextHeightPerItem = 28; // Height of each obContext item

		if (this.checked) {
			// Expand the user checkbox, show the nested content
			nestedDiv.classList.add('show');
			userDiv.classList.remove('collapsed');
			userDiv.classList.add('expanded');

			const expandHeight = 58;
			legendContainer.style.height = `${expandHeight + (numUsers * baseHeightPerUser) + ((hasVisibleUserID.length - 1) * globalState.obContext.length * obContextHeightPerItem)}px`;
		} else {
			// Collapse the user checkbox, hide the nested content
			nestedDiv.classList.remove('show');
			userDiv.classList.remove('expanded');
			userDiv.classList.add('collapsed');

			legendContainer.style.height = `${(numUsers * baseHeightPerUser) + ((hasVisibleUserID.length - 2) * globalState.obContext.length * obContextHeightPerItem)}px`;
		}

		});
	}
}


function handleContextChange(context, userId, isChecked) {
	// console.log(`Context ${context} for User ${userId} changed: ${isChecked}`);

	// Apply logic based on the context
	switch (context) {
	  case 'Object':
		globalState.viewProps[userId]["Object"] = isChecked;
		updateObjectsBasedOnSelections();

		break;

	  case 'Context':
		globalState.viewProps[userId]["Context"] = isChecked;
		updatePointCloudBasedOnSelections();
		break;

	  case 'Tracemap':
		globalState.viewProps[userId]["Tracemap"] = isChecked;
        updateMarkersBasedOnSelections();
		// plotHeatmap();
		break;

	  default:
		console.log("Handle other obContext here!");
		break;
	}
}

function plotUserSpecificBarChart() {
	const plotBox = d3.select("#plot-box2").html("");
	const margin = { top: 30, right: 20, bottom: 70, left: 70 };
	const width = plotBox.node().getBoundingClientRect().width - margin.left - margin.right;
	const height = plotBox.node().getBoundingClientRect().height - margin.top - margin.bottom;

	const svg = plotBox.append("svg")
		.attr("width", width + margin.left + margin.right)
		.attr("height", height + margin.top + margin.bottom)
		.append("g")
		.attr("transform", "translate(" + margin.left + "," + margin.top + ")");

	// Add plot title
    svg.append("text")
        .attr("x", width / 2)
        .attr("y", -4)
        .attr("text-anchor", "middle")
        .style("font-size", "16px")
        .style("font-family", "Lato")
        .style("font-weight", "bold")
        .text("User-Specific ActionReferentName");

	let allUsers = new Set();
	let userDataByActionReferentName = {};

	// Get selected users and actions from checkboxes
	const selectedUsers = Object.keys(globalState.show)
	.filter(userID => globalState.show[userID])
	.map(userID => `User${userID}`);
	const selectedActions = getSelectedTopics();

	// Filter and process data based on selected checkboxes
	globalState.finalData
		.filter(action => {
			// Check if the action is in the selected actions and users
			const isSelectedAction = selectedActions.includes(action.Name);
			const isSelectedUser = selectedUsers.includes(action.User);

			// Check if the action's time overlaps with the selected time range
			const hasTimeOverlap = action.Data.some(subAction => {
				const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
				const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration);
				return actionEndTime >= globalState.lineTimeStamp1 && actionStartTime <= globalState.lineTimeStamp2;
			});

			return isSelectedAction && isSelectedUser && hasTimeOverlap;
		})
		.forEach(action => {
			const actorName = action.User;

			action.Data
			.filter(ob => ob.ActionReferentName != null)
			.forEach(ob => {
				let ActionReferentName = ob.ActionReferentName;
				ActionReferentName = ActionReferentName.split('_')[0];
				if(globalState.useCase == "sceneNavigation" || globalState.useCase == "videoScene" ){
                    if(action.ReferentType == "Physical") {
					ActionReferentName = ActionReferentName.split("(")[1].trim();
					ActionReferentName = ActionReferentName.split(",")[0].trim();
					ActionReferentName = ActionReferentName.replace(/'/g, "");
                    }
				}else if(globalState.useCase == "maintenance"){
					ActionReferentName = ActionReferentName.match(/"([^"]+)"|([a-zA-Z\s]+)/g)?.join(' ').trim();
				}else{
					ActionReferentName = ActionReferentName.split("(")[0].trim();
				}

				if (!userDataByActionReferentName[ActionReferentName]) {
					userDataByActionReferentName[ActionReferentName] = {};
				}

				userDataByActionReferentName[ActionReferentName][actorName] = (userDataByActionReferentName[ActionReferentName][actorName] || 0) + 1;
				allUsers.add(actorName);
			})

		});

	const users = Array.from(allUsers).sort((a, b) => {
		return a.localeCompare(b);
	});
	const processedData = Object.entries(userDataByActionReferentName).map(([ActionReferentName, counts]) => ({
		ActionReferentName,
		...counts
	}));

	// Setup scales
	// Stack data for stacked bar chart
    const stack = d3.stack()
        .keys(users)
        .order(d3.stackOrderNone)
        .offset(d3.stackOffsetNone);

    const stackedData = stack(processedData);

    // Setup scales
    const x = d3.scaleBand()
        .rangeRound([0, width])
        .padding(0.8)
        .domain(processedData.map(d => d.ActionReferentName));

    const y = d3.scaleLinear()
        .domain([0, d3.max(stackedData, d => d3.max(d, d => d[1]))])
        .nice()
        .range([height, 0]);

	// Create the bars
    svg.append("g")
        .selectAll("g")
        .data(stackedData)
        .enter().append("g")
        .attr("fill", d => colorScale(d.key))
        .selectAll("rect")
        .data(d => d)
        .enter().append("rect")
        .attr("x", d => x(d.data.ActionReferentName))
        .attr("y", d => y(d[1]))
        .attr("height", d => y(d[0]) - y(d[1]))
        .attr("width",  d => Math.min(x.bandwidth(), 40))
        .on("mouseover", function (event, d) {
            const userKey = d3.select(this.parentNode).datum().key;
            d3.select(this)
                .transition()
                .duration(100)
                .attr("fill", d3.rgb(colorScale(userKey)).darker(2));

            tooltip.style("visibility", "visible")
                .text(`${userKey}: ${d.data[userKey]}`)
                .style("left", `${event.pageX + 5}px`)
                .style("top", `${event.pageY - 28}px`);
        })
        .on("mouseout", function (event, d) {
            const userKey = d3.select(this.parentNode).datum().key;
            d3.select(this)
                .transition()
                .duration(100)
                .attr("fill", colorScale(userKey));

            tooltip.style("visibility", "hidden");
        });

	// Add the axes
	svg.append("g")
		.attr("class", "axis")
		.attr("transform", `translate(0,${height})`)
		.call(d3.axisBottom(x))
		.selectAll("text")
		.style("text-anchor", "end")
		.attr("dx", "0.2em")
		.attr("dy", ".25em")
		.attr("transform", "rotate(0)")
		.style("font-size", "1.2em")
		.each(function(d) {
            const element = d3.select(this);
            const words = d.split(" ");  // Split label into words
            element.text("");  // Clear the current label

            words.forEach((word, i) => {
                element.append("tspan")
                    .text(word)
                    .attr("x", 0)
                    .attr("dy", ".9em")  // Offset subsequent lines
                    .attr("dx", "-1em")  // Adjust horizontal position slightly
                    .attr("text-anchor", "middle");
            });
        });

	svg.append("g")
		.call(d3.axisLeft(y).ticks(5))
		.selectAll(".tick text") // Select all tick texts
		.style("font-family", "Lato")
		.style("font-size", "1.2em");

		svg.append("text")
		.attr("transform", "rotate(-90)")
		.attr("y", 0 - margin.left)
		.attr("x", 0 - (height / 2))
		.attr("dy", "2em")
		.style("text-anchor", "middle")
		.text("Count")
		.style("font-size", "0.8em");

	// Add a legend
	const legend = svg.selectAll(".legend")
	.data(users)
	.enter().append("g")
	.attr("class", "legend")
	.attr("transform", (d, i) => `translate(0, ${i * 20})`);

	legend.append("rect")
	.attr("x", width - 18)
	.attr("width", 18)
	.attr("height", 18)
	.style("fill", colorScale);

	legend.append("text")
	.attr("x", width - 24)
	.attr("y", 9)
	.attr("dy", ".35em")
	.style("text-anchor", "end")
	.text(d => d)
	.style("font-size", "0.7em");

	// Tooltip for interactivity
	const tooltip = d3.select("body").append("div")
	.attr("class", "d3-tooltip")
	.style("position", "absolute")
	.style("z-index", "1000") // Set a high z-index to ensure visibility
	.style("text-align", "center")
	.style("width", "auto")
	.style("height", "auto")
	.style("padding", "8px")
	.style("font", "12px sans-serif")
	.style("background", "lightsteelblue")
	.style("border", "0px")
	.style("border-radius", "8px")
	.style("pointer-events", "none")
	.style("visibility", "hidden");
}

function plotUserSpecificDurationBarChart() {
    const plotBox = d3.select("#plot-box1").html("");
    const margin = { top: 30, right: 20, bottom: 60, left: 70 };
    const width = plotBox.node().getBoundingClientRect().width - margin.left - margin.right;
    const height = plotBox.node().getBoundingClientRect().height - margin.top - margin.bottom;

    const svg = plotBox.append("svg")
        .attr("width", width + margin.left + margin.right)
        .attr("height", height + margin.top + margin.bottom)
        .append("g")
        .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

    // Add plot title
    svg.append("text")
        .attr("x", width / 2)
        .attr("y", -4)
        .attr("text-anchor", "middle")
        .style("font-size", "16px")
        .style("font-family", "Lato")
        .style("font-weight", "bold")
        .text("User-Specific Action Durations");

    let allUsers = new Set();
    let userDurationByAction = {};

    // Get selected users and actions from checkboxes
    const selectedUsers = Object.keys(globalState.show)
        .filter(userID => globalState.show[userID])
        .map(userID => `User${userID}`);
    const selectedActions = getSelectedTopics();

    // Filter and process data based on selected checkboxes
    globalState.finalData
		.filter(action => {
			// Check if the action is in the selected actions and users
			const isSelectedAction = selectedActions.includes(action.Name);
			const isSelectedUser = selectedUsers.includes(action.User);

			// Check if the action's time overlaps with the selected time range
			const hasTimeOverlap = action.Data.some(subAction => {
				const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
				const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration);
				return actionEndTime >= globalState.lineTimeStamp1 && actionStartTime <= globalState.lineTimeStamp2;
			});

			return isSelectedAction && isSelectedUser && hasTimeOverlap;
		})
        .forEach(action => {
            const actorName = action.User;
            const actionName = action.Name;
            const duration = parseDurationToMillis(action.Duration);

            if (!userDurationByAction[actionName]) {
                userDurationByAction[actionName] = {};
            }

            userDurationByAction[actionName][actorName] = (userDurationByAction[actionName][actorName] || 0) + duration;
            allUsers.add(actorName);
        });

	const users = Array.from(allUsers).sort((a, b) => {
		return a.localeCompare(b);
	});
    const processedData = Object.entries(userDurationByAction).map(([actionName, durations]) => ({
        actionName,
        ...durations
    }));

    // Determine the maximum duration to decide the scale
    const maxDuration = d3.max(processedData, d => Math.max(...users.map(user => d[user] || 0)));
    let yLabel = "Total Duration (ms)";
    let yScaleFactor = 1;  // Default to milliseconds

    if (maxDuration > 9000) {
        yLabel = "Total Duration (s)";
        yScaleFactor = 1000;  // Convert milliseconds to seconds
    }

    // Setup scales
    const maxBandwidth = 120; // Example max bandwidth; adjust as needed
    const x0 = d3.scaleBand()
        .rangeRound([0, width])
        .paddingInner(0.1)
        .domain(processedData.map(d => d.actionName))
        .paddingOuter(0.1) // Add some outer padding to visually balance the plot
        .align(0.1); // Center-align the scale

    const x1 = d3.scaleBand()
        .padding(0.05)
        .domain(users)
        .rangeRound([0, Math.min(x0.bandwidth(), maxBandwidth)]); // Cap the bandwidth

    const y = d3.scaleLinear()
        .domain([0, maxDuration / yScaleFactor])
        .nice()
        .range([height, 0]);

    const color = colorScale;

    // Create the grouped bars
    const action = svg.selectAll(".action")
        .data(processedData)
        .enter().append("g")
        .attr("class", "g")
        .attr("transform", d => `translate(${x0(d.actionName) + x0.bandwidth() / 2 - x1.bandwidth() * users.length / 2},0)`);

    action.selectAll("rect")
        .data(d => users.map(key => ({ key, value: d[key] || 0 })))
        .enter().append("rect")
        .attr("width", d => Math.min(x1.bandwidth(), 40))
        .attr("x", d => x1(d.key))
        .attr("y", d => y(d.value / yScaleFactor))
        .attr("height", d => height - y(d.value / yScaleFactor))
        .attr("fill", d => colorScale(d.key))
        .on("mouseover", function(event, d) {
            d3.select(this)
                .transition()
                .duration(100)
                .attr("fill", d3.rgb(colorScale(d.key)).darker(2)); // Darken the color on hover

            tooltip.style("visibility", "visible")
                .text(`${d.key}: ${(d.value / yScaleFactor).toFixed(2)} ${yScaleFactor === 1000 ? 's' : 'ms'}`)
                .style("left", `${event.pageX + 5}px`)
                .style("top", `${event.pageY - 28}px`);
        })
        .on("mouseout", function(event, d) {
            d3.select(this)
                .transition()
                .duration(100)
                .attr("fill", colorScale(d.key)); // Revert to original color on mouse out

            tooltip.style("visibility", "hidden");
        });

    // Add the axes
    svg.append("g")
        .attr("class", "axis")
        .attr("transform", `translate(0,${height})`)
        .call(d3.axisBottom(x0))
        .selectAll("text")
        .style("text-anchor", "end")
        .attr("dx", "2em")
        .attr("dy", ".25em")
        .style("font-size", "1.2em")
		.each(function(d) {
            const element = d3.select(this);
            const words = d.split(" ");  // Split label into words
            element.text("");  // Clear the current label

            words.forEach((word, i) => {
                element.append("tspan")
                    .text(word)
                    .attr("x", 0)
                    .attr("dy", "1em")  // Offset subsequent lines
                    .attr("dx", "0.1em")  // Adjust horizontal position slightly
                    .attr("text-anchor", "middle");
            });
        });

    svg.append("g")
        .call(d3.axisLeft(y).ticks(5))
        .selectAll(".tick text") // Select all tick texts
        .style("font-family", "Lato")
        .style("font-size", "1.2em");

    svg.append("text")
        .attr("transform", "rotate(-90)")
        .attr("y", 0 - margin.left)
        .attr("x", 0 - (height / 2))
        .attr("dy", "2em")
        .style("text-anchor", "middle")
        .text(yLabel)
        .style("font-size", "0.8em");

    // Add a legend
    const legend = svg.selectAll(".legend")
        .data(users)
        .enter().append("g")
        .attr("class", "legend")
        .attr("transform", (d, i) => `translate(0, ${i * 20})`);

    legend.append("rect")
        .attr("x", width - 18)
        .attr("width", 18)
        .attr("height", 18)
        .style("fill", colorScale);

    legend.append("text")
        .attr("x", width - 24)
        .attr("y", 9)
        .attr("dy", ".35em")
        .style("text-anchor", "end")
        .text(d => d)
        .style("font-size", "0.7em");

    // Tooltip for interactivity
    const tooltip = d3.select("body").append("div")
        .attr("class", "d3-tooltip")
        .style("position", "absolute")
        .style("z-index", "1000")
        .style("text-align", "center")
        .style("width", "auto")
        .style("height", "auto")
        .style("padding", "8px")
        .style("font", "12px sans-serif")
        .style("background", "lightsteelblue")
        .style("border", "0px")
        .style("border-radius", "8px")
        .style("pointer-events", "none")
        .style("visibility", "hidden");
}

//Read LLM insights
function plotLLMData(){
	d3.json(globalState.llmInsightPath).then(function(data) {
		globalState.llmInsightData = data;
		createAnalysisFilter(data);
		displayInsights(data);
		callDrawBookmarks(data);
    }).catch(function(error) {
        console.error('Error loading JSON data:', error);
    });
}

function callDrawBookmarks(llmInsightData){
	let entryTS = {}
	Object.keys(llmInsightData).forEach(key => {
        const insight = llmInsightData[key];
		if(insight.timestamps.length != 0){
			entryTS[key] = insight.timestamps;
		}
	});
	drawBookmarks(entryTS);
}

function displayInsights(insightsData) {
    const insightsContainer = document.getElementById('insights-container'); // Updated to target insights-container
    insightsContainer.innerHTML = ''; // Clear previous insights, not filters

    Object.keys(insightsData).forEach(key => {
        const insight = insightsData[key];
        const insightBox = document.createElement('div');
        insightBox.className = 'insight-box';
        insightBox.id = `insight-${key}`; // Assign a unique ID to each insight

        // Create topic element
        const topicElement = document.createElement('h4');
        topicElement.className = 'insight-topic';

        // Create a key span (the number)
        const keySpan = document.createElement('span');
        keySpan.className = 'insight-key';
        keySpan.textContent = `#${key}`; // The insight number

        // Create the topic text
        const topicText = document.createElement('span');
        topicText.textContent = insight.topic;

        // Append key and topic to the topicElement
        topicElement.appendChild(keySpan);
        topicElement.appendChild(topicText);

        // Create insight element
        const insightElement = document.createElement('p');
        insightElement.textContent = insight.insight;
        insightElement.className = 'insight-content';

        insightBox.appendChild(topicElement);
        insightBox.appendChild(insightElement);
        insightsContainer.appendChild(insightBox);

		// Add click event listener for highlighting corresponding bookmark
        insightBox.addEventListener('click', function() {
			const bookmark = d3.selectAll(`#bookmark-${key} path`); // Target the path inside the bookmark group
			if (!bookmark.empty()) {
				const bookmarkDOM = document.getElementById(`bookmark-${key}`);
				bookmarkDOM.scrollIntoView({ behavior: 'smooth', block: 'center' });

				bookmark.transition()
					.duration(500)
					.attr("fill", "green")
					.transition()
					.delay(1000)  // Increased delay to show the color change for a while
					.duration(500)
					.attr("fill", "#ff9800"); // Return to original color
			}
		});
    });
}

function createAnalysisFilter(insightsData) {
    const filterContainer = document.getElementById('analysis-filter-container'); // Now targets only the filter container
    filterContainer.innerHTML = ''; // Clear any existing filters

    const analysisSet = new Set();
    Object.keys(insightsData).forEach(key => {
        insightsData[key].analyses.forEach(analysis => analysisSet.add(analysis));
    });

    analysisSet.forEach(analysis => {
        const filterTag = document.createElement('button');
        filterTag.textContent = analysis;
        filterTag.className = 'filter-tag active';
        filterTag.addEventListener('click', function() {
            this.classList.toggle('active'); // Toggle active class on click
            applyFilter(insightsData);
        });
        filterContainer.appendChild(filterTag);
    });
}

function applyFilter(insightsData) {
    const activeFilters = Array.from(document.querySelectorAll('.filter-tag.active')).map(tag => tag.textContent);

    if (activeFilters.length === 0) {
        displayInsights({}); // If no filters are active, display nothing
		callDrawBookmarks({});
    } else {
        const filteredData = {};
        Object.keys(insightsData).forEach(key => {
            const insight = insightsData[key];
            if (activeFilters.some(filter => insight.analyses.includes(filter))) {
                filteredData[key] = insight;
            }
        });
        displayInsights(filteredData);
		callDrawBookmarks(filteredData);
    }
}

function highlightAndScrollToInsight(id) {
    const insightElement = document.getElementById(`insight-${id}`);
    if (insightElement) {
        // Scroll into view
        insightElement.scrollIntoView({ behavior: 'smooth', block: 'center' });

        // Highlight the element
        insightElement.classList.add('highlight-insight');

        // Remove highlight after animation
        setTimeout(() => {
            insightElement.classList.remove('highlight-insight');
        }, 2000); // Keep it highlighted for 2 seconds
    }
}

function getSelectedTopics() {
    const topicCheckboxes = document.querySelectorAll('.topic-checkbox:checked');
    let selectedActions = [];

    topicCheckboxes.forEach(checkbox => {
        if (!checkbox.disabled) {  // Only include enabled checkboxes
            selectedActions.push(checkbox.value);
        }
    });
    return selectedActions;
}

function parseTimeToMillis(customString) {
  let [dateStr, timeStr, milliStr] = customString.split('_');

  // Further split into year, month, day, hours, minutes, seconds
  let year = parseInt(dateStr.slice(0, 2), 10) + 2000; // Assuming '24' is 2024
  let month = parseInt(dateStr.slice(2, 4), 10) - 1; // Month is 0-indexed in JS
  let day = parseInt(dateStr.slice(4, 6), 10);

//   let hours = parseInt(timeStr.slice(0, 2), 10);
  let hours = parseInt(timeStr.slice(0, 2), 10) + 4 ;
  let minutes = parseInt(timeStr.slice(2, 4), 10);
  let seconds = parseInt(timeStr.slice(4, 6), 10);

  // Milliseconds are straightforward, just need to parse
  let milliseconds = parseInt(milliStr, 10);

  // Create the Date object
  let date = new Date(Date.UTC(year, month, day, hours, minutes, seconds));

  // Return the time in milliseconds since Unix epoch
  let timeInMillis = date.getTime();
  return timeInMillis;
}

function updateSpatialView(nextTimestamp){
    globalState.lineTimeStamp1 = nextTimestamp;
    globalState.lineTimeStamp2 = nextTimestamp + 10000;
      updatePointCloudBasedOnSelections();
      updateObjectsBasedOnSelections();
      updateMarkersBasedOnSelections();
    return ;

}

function parseDurationToMillis(durationString) {
    // Split the string by underscores
    const parts = durationString.split('_');

    // Extract the hours, minutes, seconds, and microseconds from the string
    const hours = parseInt(parts[1].slice(0, 2), 10);
    const minutes = parseInt(parts[1].slice(2, 4), 10);
    const seconds = parseInt(parts[1].slice(4, 6), 10);
    const microseconds = parseInt(parts[2], 10);

    // Convert the extracted time parts to milliseconds
    const totalMillis = (hours * 60 * 60 * 1000) + (minutes * 60 * 1000) + (seconds * 1000) + (microseconds / 1000);

    return totalMillis;
}

function initializeOrUpdateSpeechBox() {
    // Use selected action names from the toolbar
    const selectedActions = getSelectedTopics(); 
    const data = globalState.finalData;

	const visibleUserIDs = Object.keys(globalState.show).filter(userID => globalState.show[userID]);

    const container = document.getElementById("speech-box");
    const hierToolbar = document.getElementById('hier-toolbar');
    let offsetHeight = hierToolbar.offsetHeight;
    container.style.top = `${offsetHeight}px`;

    let speechBoxesContainer = document.getElementById("speech-boxes-container");
    if (!speechBoxesContainer) {
        speechBoxesContainer = document.createElement('div');
        speechBoxesContainer.id = "speech-boxes-container";
        container.appendChild(speechBoxesContainer);
    } else {
        speechBoxesContainer.innerHTML = ''; // Clear previous entries
    }

    const timeFormat = d3.timeFormat("%b %d %I:%M:%S %p");
    let rangeDisplay = document.querySelector('.time-range-display-speechbox');
    if (!rangeDisplay) {
        rangeDisplay = document.createElement('div');
        rangeDisplay.className = 'time-range-display-speechbox';
        container.appendChild(rangeDisplay);
    }
    rangeDisplay.innerHTML = `<strong>Selected Time Range: ${timeFormat(new Date(globalState.lineTimeStamp1))} - ${timeFormat(new Date(globalState.lineTimeStamp2))}</strong>`;

	let actionsToDisplay = data.filter(action => {
		// Check if the action name includes any of the visible user IDs
		const hasVisibleUserID = visibleUserIDs.some(userID => action.User.includes(userID));

		return hasVisibleUserID && selectedActions.includes(action.Name) && action.Data.some(subAction => {
			const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
			const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration);
			return actionEndTime >= globalState.lineTimeStamp1 && actionStartTime <= globalState.lineTimeStamp2;
		});
	});

	
    const totalResults = actionsToDisplay.reduce((count, action) => count + action.Data.length, 0);
    let totalResultsDisplay = document.createElement('div');
    totalResultsDisplay.className = 'total-results-display';
    totalResultsDisplay.innerHTML = `<strong>About ${totalResults} results...</strong>`;
    speechBoxesContainer.appendChild(totalResultsDisplay); // Add the total results before speech boxes

    // Display each action in the speech box
    actionsToDisplay.forEach(action => {
        action.Data.forEach(subAction => {
            const speechBox = createSpeechBox(action, subAction);
            if (speechBox) {
                speechBoxesContainer.appendChild(speechBox);
            }
        });
    });
}

function updateXRSnapshot(){
	const container = document.getElementById('user-xr-snapshot');
	container.innerHTML = ''; // Clear previous content

	// Create a title element
	const titleElement = document.createElement('div');
	titleElement.style.textAlign = 'center';
	titleElement.id = 'imageTitle';
	container.appendChild(titleElement);

	const data = globalState.finalData;
	const visibleUserIDs = Object.keys(globalState.show).filter(userID => globalState.show[userID]);
	const selectedActions = getSelectedTopics();
	let imgPaths = [];

	let actionsToDisplay = data.filter(action => {
		const hasVisibleUserID = visibleUserIDs.some(userID => action.User.includes(userID));
		const filteredSubActions = action.Data.filter(subAction => {
			const actionStartTime = parseTimeToMillis(subAction.ActionInvokeTimestamp);
			const actionEndTime = actionStartTime + parseDurationToMillis(action.Duration);

			const isInTimeRange = actionEndTime >= globalState.lineTimeStamp1 &&
				actionStartTime <= globalState.lineTimeStamp2;

			const containsPng = subAction.ActionReferentBody && subAction.ActionReferentBody.includes("png");

			if (isInTimeRange && containsPng) {
				imgPaths.push(subAction.ActionReferentBody); // Add to imgPaths if it contains 'png'
			}

			return isInTimeRange && containsPng;
		});

		return hasVisibleUserID && selectedActions.includes(action.Name) && filteredSubActions.length > 0;
	});

	if (imgPaths.length >= 1) {
		container.style.display = 'block';
		const imageWrapper = document.createElement('div');
		imageWrapper.style.position = 'relative';

		imageWrapper.style.maxWidth = '100%';  // Set max width to 100% of the container
        imageWrapper.style.maxHeight = '100%';  // Set max height to 100% of the container
		imageWrapper.style.margin = 'auto';
		imageWrapper.style.overflow = 'hidden'; // Ensures the content doesn't overflow
		imageWrapper.style.textAlign = 'center'; // Center the arrows relative to the image

		imgPaths.forEach((imagePath, index) => {
			imagePath = globalState.objFilePath + '\\' + imagePath;
			const img = document.createElement('img');
			img.src = imagePath;
			img.alt = "Raw Capture Image";
			img.style.maxWidth = '100%';
			img.style.maxHeight = '100%'; // Ensure it fits vertically too
			img.style.objectFit = 'contain';
			img.style.display = index === 0 ? 'block' : 'none';
			imageWrapper.appendChild(img);
		});

		container.appendChild(imageWrapper);

		addNavigationArrows(imageWrapper, imgPaths);
	} else {
		// titleElement.innerHTML = 'No images available!';
		container.style.display = 'none';
	}
}

function addNavigationArrows(imageWrapper, imgPaths) {
	const prevArrow = document.createElement('button');
	prevArrow.innerHTML = '&#10094;';
	prevArrow.style.position = 'absolute';
	prevArrow.style.top = '50%';
	prevArrow.style.left = '5px';
	prevArrow.style.transform = 'translateY(-50%)'; // Center vertically
	prevArrow.style.fontSize = '2em'; // Adjust size relative to image
	prevArrow.style.background = 'rgba(0, 0, 0, 0.5)'; // Semi-transparent background
	prevArrow.style.color = 'white'; // Arrow color
	prevArrow.style.border = 'none'; // Remove border
	prevArrow.style.padding = '1px';
	prevArrow.style.cursor = 'pointer';
	prevArrow.style.zIndex = '10';

	const nextArrow = document.createElement('button');
	nextArrow.innerHTML = '&#10095;';
	nextArrow.style.position = 'absolute';
	nextArrow.style.top = '50%';
	nextArrow.style.right = '5px';
	nextArrow.style.transform = 'translateY(-50%)';
	nextArrow.style.fontSize = '2em';
	nextArrow.style.background = 'rgba(0, 0, 0, 0.5)';
	nextArrow.style.color = 'white';
	nextArrow.style.border = 'none';
	nextArrow.style.padding = '5px';
	nextArrow.style.cursor = 'pointer';
	nextArrow.style.zIndex = '10';

	let currentIndex = 0;
	prevArrow.onclick = () => {
	  currentIndex = (currentIndex - 1 + imgPaths.length) % imgPaths.length;
	  changeImage(currentIndex, imageWrapper, imgPaths);
	};

	nextArrow.onclick = () => {
	  currentIndex = (currentIndex + 1) % imgPaths.length;
	  changeImage(currentIndex, imageWrapper, imgPaths);
	};

	imageWrapper.appendChild(prevArrow);
	imageWrapper.appendChild(nextArrow);
}

function changeImage(index, imageWrapper, imgPaths) {
	Array.from(imageWrapper.getElementsByTagName('img')).forEach((img, imgIndex) => {
		img.style.display = imgIndex === index ? 'block' : 'none';
	});
}

function formatLocation(locationString) {
    const location = parseLocation(locationString);

    if (!location) {
        return `<strong>Location:</strong><br>${formattedLocation}`;
    }

    const position = `${location.x.toFixed(3)}, ${location.y.toFixed(3)}, ${location.z.toFixed(3)}`;
    const orientation = `${location.pitch.toFixed(3)}, ${location.yaw.toFixed(3)}, ${location.roll.toFixed(3)}`;

    return `
        <strong>Position (X, Y, Z):</strong> ${position}<br>
        <strong>Orientation (Pitch, Yaw, Roll):</strong> ${orientation}
    `;
}

function createSpeechBox(action, subAction) {

    const speechBox = document.createElement('div');
    speechBox.className = 'speech-box';

    const titleContainer = document.createElement('div');
    titleContainer.style.display = 'flex';
    titleContainer.style.justifyContent = 'space-between';
    titleContainer.style.alignItems = 'center';
    titleContainer.style.marginBottom = '10px';

    const title = document.createElement('h4');
    title.textContent = `Action: ${action.Name}`;
    title.style.margin = '0'; // Remove margin for better alignment
	title.style.marginLeft = '10px'

    // Get the background color for the user
    const userColor = colorScale(action.User); // Default to gray if user is not in the mapping

    // Create user label
    const userLabel = document.createElement('div');
    userLabel.textContent = action.User;
    userLabel.className = 'user-label';
    userLabel.style.backgroundColor = userColor;
    userLabel.style.padding = '5px 10px';
    userLabel.style.borderRadius = '5px';
    userLabel.style.color = 'white';

    // Position the user label to the right
    userLabel.style.marginLeft = 'auto'; // Pushes userLabel to the right

    // Append the title and user label to the container
    titleContainer.appendChild(title);
    titleContainer.appendChild(userLabel);

    // Append the container to the speech box
    speechBox.appendChild(titleContainer);
    // Format Location as Position (X, Y, Z) and Orientation (Roll, Pitch, Yaw)
    const locationString = subAction.ActionInvokeLocation;
    const formattedLocation = formatLocation(locationString);

	// <strong>Location:</strong> ${subAction.ActionInvokeLocation}<br>
	// <strong>Location:</strong><br>${formattedLocation}<br>

    // Adding more detailed information
    const details = document.createElement('div');
	// Highlight the Intent field
    const intentDiv = document.createElement('div');
    // intentDiv.style.backgroundColor = '#d3d3d3';  // Cool Mint for highlighting
    intentDiv.style.backgroundColor = 'rgba(235,235,235,1.0)';
    intentDiv.style.color = 'black';
    intentDiv.style.padding = '4px';
    intentDiv.style.borderRadius = '5px';
    intentDiv.style.fontSize = '1em';
    intentDiv.style.borderRadius = '8px';
    intentDiv.innerHTML = `<strong>Intent:</strong> ${action.Intent}`;
	let otherDetails ;
	if (action.TriggerSource === "Audio")
	{
		otherDetails = `
        <strong>Timestamp:</strong> ${new Date(parseTimeToMillis(subAction.ActionInvokeTimestamp)).toLocaleString()}<br>
        <strong>Duration:</strong> ${parseDurationToMillis(action.Duration)} ms<br>
        <strong>Trigger Source:</strong> ${action.TriggerSource}<br>
        <strong>Transcribed Text:</strong> ${subAction.ActionReferentBody}<br>

    `;
	}
	else{
        if (subAction.ActionReferentBody){
            otherDetails = `
            <strong>Timestamp:</strong> ${new Date(parseTimeToMillis(subAction.ActionInvokeTimestamp)).toLocaleString()}<br>
            <strong>Duration:</strong> ${parseDurationToMillis(action.Duration)} ms<br>
            <strong>Trigger Source:</strong> ${action.TriggerSource}<br>
            <strong>Referent Name:</strong> ${subAction.ActionReferentName || 'N/A'}<br>
            `;
            }
            else {
            otherDetails = `
            <strong>Timestamp:</strong> ${new Date(parseTimeToMillis(subAction.ActionInvokeTimestamp)).toLocaleString()}<br>
            <strong>Duration:</strong> ${parseDurationToMillis(action.Duration)} ms<br>
            <strong>Trigger Source:</strong> ${action.TriggerSource}<br>
            `;
            }
        }
	details.appendChild(intentDiv);
    details.innerHTML += otherDetails;

    speechBox.appendChild(details);

    return speechBox;
}


function updateRangeDisplay(time1, time2) {
	const indicatorSVG = d3.select("#indicator-svg");
	indicatorSVG.selectAll("rect.shading").remove();
	const svg = d3.select("#temporal-view");

	const line1X = parseFloat(d3.select('#time-indicator-line1').attr('x1'));
	const line2X = parseFloat(d3.select('#time-indicator-line2').attr('x1'));
	const yStart = parseFloat(d3.select('#time-indicator-line1').attr('y1'));
	const yEnd = parseFloat(d3.select('#time-indicator-line1').attr('y2'));
	const height = yEnd - yStart;
	const xStart = Math.min(line1X, line2X);
	const xEnd = Math.max(line1X, line2X);
	const shadingWidth = xEnd - xStart;

	indicatorSVG.append("rect")
		.attr("class", "shading")
		.attr("x", xStart)
		.attr("y", yStart)
		.attr("width", shadingWidth)
		.attr("height", height)
		.attr("fill", "#9e9e9e")
		.attr("fill-opacity", 0.5);
	const timeFormat = d3.timeFormat("%b %d %I:%M:%S %p");
    const rangeDisplay = document.getElementById("range-display");
	if (rangeDisplay) {
	  rangeDisplay.textContent = `Selected Time Range: ${timeFormat(new Date(time1))} - ${timeFormat(new Date(time2))}`;
	}
	initializeShadedAreaDrag();
}

function initializeShadedAreaDrag() {
    const indicatorSVG = d3.select("#indicator-svg");
    const shadedArea = indicatorSVG.select(".shading");
    let dragStartX = null;

    const dragstarted = (event) => {
        dragStartX = event.x;
    };

    const dragged = (event) => {
        const dx = event.x - dragStartX;
        const line1 = indicatorSVG.select("#time-indicator-line1");
        const line2 = indicatorSVG.select("#time-indicator-line2");
        const circle1 = indicatorSVG.select("#time-indicator-circle1");
        const circle2 = indicatorSVG.select("#time-indicator-circle2");

        // Update positions of line1 and line2
        let line1X = parseFloat(line1.attr("x1")) + dx;
        let line2X = parseFloat(line2.attr("x1")) + dx;

        line1.attr("x1", line1X).attr("x2", line1X);
        line2.attr("x1", line2X).attr("x2", line2X);
        circle1.attr("cx", line1X);
        circle2.attr("cx", line2X);

        // Update shaded area between line1 and line2
        updateShadedArea(line1X, line2X);

        // Update timestamps
        const newLine1Timestamp = x.invert(line1X - buffer).getTime();
        const newLine2Timestamp = x.invert(line2X - buffer).getTime();
        globalState.lineTimeStamp1 = newLine1Timestamp;
        globalState.lineTimeStamp2 = newLine2Timestamp;

        updateRangeDisplay(newLine1Timestamp, newLine2Timestamp);

        // Update visual elements based on new positions
        for (let i = 0; i < numUsers; i++) {
            updateUserDevice(i);
            updateLeftControl(i);
            updateRightControl(i);
        }

        // plotHeatmap();
        updatePointCloudBasedOnSelections();
        updateObjectsBasedOnSelections();
        updateMarkersBasedOnSelections();

        dragStartX = event.x;
    };

    const dragended = () => {
        generateHierToolBar();
        plotUserSpecificBarChart();
        plotUserSpecificDurationBarChart();
        // plotHeatmap();
        updateObjectsBasedOnSelections();
        updatePointCloudBasedOnSelections();
        updateMarkersBasedOnSelections();
		initializeOrUpdateSpeechBox();
		updateXRSnapshot();
    };

    const drag = d3.drag()
        .on("start", dragstarted)
        .on("drag", dragged)
        .on("end", dragended);

    shadedArea.call(drag);
}

function updateShadedArea(line1X, line2X) {
    const indicatorSVG = d3.select("#indicator-svg");
    const shadedArea = indicatorSVG.select(".shading");

    // Set the shaded area's position and width based on line1 and line2 positions
    const startX = Math.min(line1X, line2X);
    const endX = Math.max(line1X, line2X);
    shadedArea.attr("x", startX).attr("width", endX - startX);
}

function updateTimeDisplay(timestamp, startTime) {
	const elapsedMs = timestamp - startTime;
	const elapsedMinutes = Math.floor(elapsedMs / 60000); // Convert to minutes
	const elapsedSeconds = Math.floor((elapsedMs % 60000) / 1000); // Remaining seconds
	const milliseconds = Math.round((elapsedMs % 1000) / 10);
	const date = new Date(timestamp);
	const hours = date.getHours().toString().padStart(2, '0');
	const minutes = date.getMinutes().toString().padStart(2, '0');
	const seconds = date.getSeconds().toString().padStart(2, '0');
	const milliseconds2 = date.getMilliseconds().toString().padStart(3, '0');

	const timeDisplay = document.getElementById('timeDisplay');
	if (timeDisplay) {
		timeDisplay.textContent = `${hours}:${minutes}:${seconds}`;
	}
}

function createSharedAxis() {
	const { globalStartTime, globalEndTime, bins, unit } = globalState;
	const temporalViewContainer = d3.select("#temporal-view");
	const minWidth = document.getElementById('temporal-view').clientWidth - margin.right - margin.left;
	let sharedAxisContainer = temporalViewContainer.select("#shared-axis-container");
	if (sharedAxisContainer.empty()) {
	  sharedAxisContainer = temporalViewContainer.append("div").attr("id", "shared-axis-container");
	}

	sharedAxisContainer.html("");
	const timeFormat = d3.timeFormat("%I:%M:%S");
	const totalDuration = globalEndTime - globalStartTime;
	let intervalSizeMillis;
	if (unit === 'minutes') {
	  intervalSizeMillis = bins * 60 * 1000; // Convert minutes to milliseconds
	} else {
	  intervalSizeMillis = bins * 1000; // Convert seconds to milliseconds
	}

	const totalDurationMillis = globalEndTime - globalStartTime;
	const numberOfIntervals = Math.ceil(totalDurationMillis / intervalSizeMillis);
	const widthPerInterval = 100; // Fixed width for each interval

	const intervalDuration = totalDuration * (bins / 100);
	// console.log("interval duration " + intervalDuration/(1000 * 60));
	// const numberOfIntervals = Math.ceil(100 / bins);
	globalState.dynamicWidth = numberOfIntervals * widthPerInterval;
	// let localDynamicWidth = numberOfIntervals * widthPerInterval;
	globalState.dynamicWidth = Math.max(globalState.dynamicWidth, minWidth);

	// Adjust the scale to cover the dynamic width
	x = d3.scaleTime()
		.domain([new Date(globalStartTime), new Date(globalEndTime)])
		.range([0, globalState.dynamicWidth]);

		const xAxis = d3.axisTop(x)
		.ticks(d3.timeMillisecond.every(intervalSizeMillis))
		.tickFormat(timeFormat);

	// Create SVG for the axis
	const svg = sharedAxisContainer.append("svg")
		.attr("width", globalState.dynamicWidth + margin.left + margin.right)
		.attr("height", 50)
		.append("g")
		.attr("transform", `translate(${margin.left}, ${margin.top})`);

	svg.append("g")
		.attr("class", "x-axis")
		.call(xAxis)
		.selectAll("text")
    	.style("font-size", "14px");

	// Enable horizontal scrolling
	// sharedAxisContainer.style("overflow-x", "auto").style("max-width", "100%");
}


function onWindowResize() {
	const spatialView = document.getElementById('spatial-view');
	globalState.camera.aspect = spatialView.clientWidth / spatialView.clientHeight;
	globalState.camera.updateProjectionMatrix();
	globalState.renderer.setSize(spatialView.clientWidth, spatialView.clientHeight);
}

async function initialize() {
	await initializeScene();
	const binsDropdown = document.getElementById('binsDropdown');
	globalState.bins = binsDropdown.value;

	createSharedAxis();
	createPlotTemporal();
	initHierToolBar();
	generateHierToolBar();

	createLines(globalState.lineTimeStamp1, globalState.lineTimeStamp2);
	document.querySelectorAll('.topic-checkbox').forEach(checkbox => {
	  checkbox.checked = true;
	  checkbox.dispatchEvent(new Event('change'));
	});
	// updateInterestBox();
	initializeOrUpdateSpeechBox();
    if(!logMode.infoVisCollab1){
	    plotLLMData();
    }
	// plotHeatmap();

	updatePointCloudBasedOnSelections();
	updateObjectsBasedOnSelections();
    updateMarkersBasedOnSelections();
	plotUserSpecificBarChart();
	plotUserSpecificDurationBarChart();
}

initialize();
globalState.camera.updateProjectionMatrix();
onWindowResize();


function animate() {
	requestAnimationFrame(animate);
	globalState.controls.update();
	globalState.renderer.render(globalState.scene, globalState.camera);
}
animate();