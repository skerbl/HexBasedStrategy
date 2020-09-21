sampler2D _HexCellData;
float4 _HexCellData_TexelSize;

// Enables full visibility for everything when Edit Mode is active.
float4 FilterCellData(float4 data) 
{
#if defined(HEX_MAP_EDIT_MODE)
	data.x = 1;
#endif
	return data;
}

float4 GetCellData(appdata_full v, int index)
{
	// Construct UV-coordinates by dividing index by texture width (= multiply by _TexelSize). 
	// Add 0.5 to get the center of the pixel.
	float2 uv;
	uv.x = (v.texcoord2[index] + 0.5) * _HexCellData_TexelSize.x;

	// This results in a number of the format Z.U, where Z is the row number, and U is the U coord.
	// Floor the number to get the row, then subtract that from the number
	float row = floor(uv.x);
	uv.x -= row;

	// V coord can be found by dividing the row by texture height (= multiply by _TexelSize)
	uv.y = (row + 0.5) * _HexCellData_TexelSize.y;

	// Set mipmap, with extra coords set to zero
	float4 data = tex2Dlod(_HexCellData, float4(uv, 0, 0));

	// GPU automatically converts the values into floats of range 0–1.
	// Multiply by 255 to convert back to single byte integer.
	data.w *= 255;
	return FilterCellData(data);
}

float4 GetCellData(float2 cellDataCoordinates) 
{
	float2 uv = cellDataCoordinates + 0.5;
	uv.x *= _HexCellData_TexelSize.x;
	uv.y *= _HexCellData_TexelSize.y;
	return FilterCellData(tex2Dlod(_HexCellData, float4(uv, 0, 0)));
}