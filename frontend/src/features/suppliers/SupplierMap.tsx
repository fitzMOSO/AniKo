import { divIcon } from 'leaflet'
import { useTranslation } from 'react-i18next'
import { MapContainer, Marker, Popup, TileLayer } from 'react-leaflet'
import { formatDistance } from '@/lib/format'
import type { LatLng } from '@/lib/geo'
import { MAP_HEIGHT_CLASS } from './mapLayout'
import type { NearbySupplier } from './types'

/*
 * Everything Leaflet lives in this module and nowhere else, because this module
 * is the one behind `SupplierMap.lazy`. The stylesheet is imported here for the
 * same reason: Leaflet cannot position its panes without it, but pulled in from
 * a component that ships in the entry chunk it would land in the entry CSS and
 * be paid for by every visitor, including the ones who never see a tile.
 */
import 'leaflet/dist/leaflet.css'

/*
 * Zoom 7 fits Benguet in the north to Laguna in the south around a Quezon City
 * centre. Deliberately a fixed centre and zoom rather than `fitBounds`: under
 * jsdom the container measures 0x0, and a zoom derived from a zero-sized
 * viewport is not a number any test could sensibly assert against.
 */
const ZOOM = 7

const PIN_WIDTH = 24
const PIN_HEIGHT = 32

/**
 * Marker icons are built with `divIcon`, never Leaflet's default.
 *
 * Vite inlines `marker-icon.png` as a base64 data URI, which defeats Leaflet's
 * icon-path detection — its `_stripUrl` regex wants a URL ending in
 * `marker-icon.png` — and the marker `src` degrades to a bare broken string.
 * That failure appears only in `vite build`, never in `vite dev`, so it is the
 * kind of bug a green dev server certifies as absent. `divIcon` touches none of
 * that machinery, drops the PNGs entirely, and lets the pin take its colour
 * from the design tokens instead of shipping a fixed blue bitmap.
 *
 * The cost of `divIcon` is that the marker element is a DIV, and Leaflet
 * applies a Marker's `alt` option only when that element is an IMG (see
 * `Marker._initIcon`). `alt` is silently dropped here. The accessible name has
 * to come from the icon's own content instead — which means the supplier name
 * is interpolated into an HTML string, and must be escaped on the way in.
 */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function supplierIcon(label: string) {
  return divIcon({
    // Empty, not the default `leaflet-div-icon`, which draws a white box.
    className: '',
    html: `
      <span class="sr-only">${escapeHtml(label)}</span>
      <svg viewBox="0 0 24 32" width="${PIN_WIDTH}" height="${PIN_HEIGHT}" aria-hidden="true" focusable="false">
        <path d="M12 0a12 12 0 0 0-12 12c0 8.5 12 20 12 20s12-11.5 12-20A12 12 0 0 0 12 0z" fill="var(--color-primary)"/>
        <circle cx="12" cy="12" r="4.5" fill="var(--color-surface)"/>
      </svg>`,
    iconSize: [PIN_WIDTH, PIN_HEIGHT],
    /*
     * `divIcon` anchors to the icon's centre by default, which would put every
     * supplier half a pin north of where it actually is. The tip is the part
     * that means "here", so the anchor is the bottom centre.
     */
    iconAnchor: [PIN_WIDTH / 2, PIN_HEIGHT],
    popupAnchor: [0, -PIN_HEIGHT],
  })
}

const ORIGIN_SIZE = 18

/**
 * The buyer is marked, and marked differently.
 *
 * Every distance on this panel is measured from one point, and a map that
 * shows the measured without the measured-from asks the reader to hold the
 * origin in their head. But the buyer is not a supplier and must never be
 * mistaken for one, so the distinction is carried by SHAPE first — a dot, not
 * a teardrop — with colour as the second channel rather than the only one.
 * The dark ring keeps its edge legible over both pale and dark tiles.
 */
function originIcon(label: string) {
  return divIcon({
    className: '',
    html: `
      <span class="sr-only">${escapeHtml(label)}</span>
      <svg viewBox="0 0 18 18" width="${ORIGIN_SIZE}" height="${ORIGIN_SIZE}" aria-hidden="true" focusable="false">
        <circle cx="9" cy="9" r="9" fill="var(--color-primary)"/>
        <circle cx="9" cy="9" r="4.5" fill="var(--color-highlight)"/>
      </svg>`,
    iconSize: [ORIGIN_SIZE, ORIGIN_SIZE],
    // A dot means the point it is centred on, so here the centre IS the anchor.
    iconAnchor: [ORIGIN_SIZE / 2, ORIGIN_SIZE / 2],
  })
}

/**
 * The map is an enhancement over the list, never a replacement for it. It takes
 * the suppliers the panel already resolved rather than calling the hook again,
 * which is what makes "a pin and its row can never disagree" structural instead
 * of a thing someone has to remember.
 */
export function SupplierMap({
  suppliers,
  origin,
}: {
  suppliers: NearbySupplier[]
  origin: LatLng
}) {
  const { t, i18n } = useTranslation()

  return (
    // MapContainer forwards only className/id/style to its div, so the label
    // has to live on a wrapper rather than on the map element itself.
    <div role="group" aria-label={t('suppliers.map_label')}>
      <MapContainer
        center={[origin.lat, origin.lng]}
        zoom={ZOOM}
        // A map that swallows the page scroll is a trap on a dashboard this
        // tall. Dragging and the zoom buttons still work.
        scrollWheelZoom={false}
        className={`${MAP_HEIGHT_CLASS} w-full rounded-lg`}
      >
        <TileLayer
          url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
          // Licence obligation under ODbL, not decoration. Removing this is a
          // terms-of-use breach, not a style tweak.
          attribution={t('suppliers.map_attribution')}
        />

        <Marker
          position={[origin.lat, origin.lng]}
          icon={originIcon(t('suppliers.map_origin'))}
          title={t('suppliers.map_origin')}
        />

        {suppliers.map((supplier) => (
          <Marker
            key={supplier.id}
            position={[supplier.location.lat, supplier.location.lng]}
            icon={supplierIcon(supplier.name)}
            title={supplier.name}
          >
            {/*
              Three block elements, not one blob split by <br>. The popup says
              the same three things the row says, and each needs to be its own
              node so it can be read, and asserted on, as its own line.
            */}
            <Popup>
              <p className="font-semibold text-primary">{supplier.name}</p>
              <p className="text-muted-fg">{supplier.region}</p>
              <p className="text-muted-fg">
                {t('suppliers.distance_away', {
                  distance: formatDistance(supplier.distanceKm, i18n.language),
                })}
              </p>
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  )
}
