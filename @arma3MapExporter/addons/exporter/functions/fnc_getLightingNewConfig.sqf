private _lightingNewConfig = configFile/"cfgWorlds"/worldName/"Weather"/"LightingNew";
private _latitude = -1 * getNumber (configFile/"CfgWorlds"/worldName/"latitude");
private _day = 360 * (dateToNumber date);
private _hour = (dayTime/24) * 360;
private _currentSunAngle = ((12 * (cos _day) - 78) * (cos _latitude) * (cos _hour)) - (24 * (sin _latitude) * (cos _day));

private _candidateLightingNewClass = configNull;
private _candidateOvercast = 0;
private _candidateSunAngle = 0;

// Find the LightingNew child that have both :
// - Smaller overcast (as we forced overcast to 0)
// - Smaller sunAngle that is larger than _currentSunAngle

for "_i" from (0) to ((count _lightingNewConfig) - 1) do
{
	private _lightingNewClass = _lightingNewConfig select _i;

	if (isClass _lightingNewClass) then
	{
		private _height    = getNumber (_lightingNewClass/"height");
		private _sunOrMoon = getNumber (_lightingNewClass/"sunOrMoon");

		if ( _height >= 0 &&  _sunOrMoon == 1 ) then {
		
			private _overcast  = getNumber (_lightingNewClass/"overcast");
			private _sunAngle  = getNumber (_lightingNewClass/"sunAngle");
			
			diag_log [_lightingNewClass, _candidateOvercast, _overcast, _candidateSunAngle, _currentSunAngle, _sunAngle ];
			
			
			if ( (_sunAngle > _currentSunAngle ) 
				&& ((isNull _candidateLightingNewClass) || 
				    {(_sunAngle <= _candidateSunAngle) && (_overcast <= _candidateOvercast)})) then {
					
				_candidateLightingNewClass = _lightingNewClass;
				_candidateOvercast = _overcast;
				_candidateSunAngle = _sunAngle;
			};

		};
	};
};

if ( isNull _candidateLightingNewClass ) then 
{
	// Otherwise just pick the one with the highest apertureStandard, which should be the brightest one	
	private _candidateApertureStandard = 0;
	for "_i" from (0) to ((count _lightingNewConfig) - 1) do
	{
		private _lightingNewClass = _lightingNewConfig select _i;
		if (isClass _lightingNewClass) then
		{
			private _apertureStandard   = getNumber (_lightingNewClass/"apertureStandard");
			
			if ( _apertureStandard > _candidateApertureStandard ) then {
				_candidateApertureStandard = _apertureStandard;
				_candidateLightingNewClass = _lightingNewClass;
			};
		};
	};
};



_candidateLightingNewClass
