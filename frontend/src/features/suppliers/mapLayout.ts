/**
 * The map's height, in one place.
 *
 * The skeleton must be exactly as tall as the map or the panel jumps when the
 * Leaflet chunk lands. It cannot import that height from `SupplierMap` — that
 * import would pull Leaflet back into the entry chunk and undo the split the
 * skeleton exists to cover — so the constant lives in this Leaflet-free module
 * and both sides read it from here rather than each keeping their own copy.
 */
export const MAP_HEIGHT_CLASS = 'h-[240px]'
