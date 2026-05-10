#include "script_component.hpp"

class CfgPatches {
    class ADDON {
        name = QUOTE(COMPONENT);
        units[] = {};
        weapons[] = {};
        requiredVersion = REQUIRED_VERSION;
        requiredAddons[] = {"a3me_main"};
        author = "AUTHOR";
        VERSION_CONFIG;
    };
};


class RscMapControl;

#include "CfgEventHandlers.hpp"

class A3ME_MapExportDisplay {
    idd = 9801;
    class controls {
        class Map : RscMapControl {
            idc = 51;
            maxSatelliteAlpha = 0;     // force topographic — no satellite layer
            x = "safeZoneXAbs";
            y = "safeZoneY";
            w = "safeZoneWAbs";
            h = "safeZoneH";

            // Style from ACE3

            // From Arma 2
            colorTracks[] = {0.2,0.13,0,1};
            colorTracksFill[] = {1,0.88,0.65,0.3};
            colorRoads[] = {0.2,0.13,0,1};
            colorRoadsFill[] = {1,0.88,0.65,1};
            colorMainRoads[] = {0.0,0.0,0.0,1};
            colorMainRoadsFill[] = {0.94,0.69,0.2,1};
            colorRailWay[] = {0.8,0.2,0,1};
            colorGrid[] = {0.05,0.1,0,0.6};
            colorGridMap[] = {0.05,0.1,0,0.4};

            // From ACE2
            colorBackground[] = {0.929412, 0.929412, 0.929412, 1.0};
            colorOutside[] = {0.929412, 0.929412, 0.929412, 1.0};
            colorCountlines[] = {0.647059, 0.533333, 0.286275, 1};
            colorMainCountlines[] = {0.858824, 0, 0,1};
            colorForest[] = {0.6, 0.8, 0.2, 0.25};
            colorLevels[] = {0.0, 0.0, 0.0, 1.0};
            colorRocks[] = {0.50, 0.50, 0.50, 0.50};

            sizeExLevel = 0.03;
            showCountourInterval = 1; // refs #13673

            sizeExGrid = 0.032;
        };
    };
};

class CfgDifficultyPresets
{
	defaultPreset = "Veteran";
};