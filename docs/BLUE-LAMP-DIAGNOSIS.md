# Blue table lamp — live diagnosis, 2026-10-18

Owner returned to the lamp at night and supplied a screenshot showing the blue
table lantern between two orange lanterns. Read-only neutral API capture and a
passive read of the known immutable, credential-free Hue HUD state were used.
The process was not suspended or modified. No lamp commands or settings changes
were issued by this investigation.

## Source association and visibility

Capture 3299 / envelope 11401: nearest source at
`(-10510.692, 611.6332, -4371.4375)`, 1.735 game units from the player, selected by
the renderer. Projection through that capture's paired camera is 46.10% across
and 51.44% down the image, consistent with the blue table lantern in the owner's
screenshot. This is a positional association, not a durable engine object ID.
The source is `clear`, physics-ray-fan 9/9, measurement age 62 ms.

Three subsequent progressing captures (5187, 5189, 5190) retain that position,
all clear 9/9, visibility ages 15/32/16 ms, capture ages 31/63/47 ms. The earlier
October 17 blocked blue candidate was a different, much farther source. Do not
attribute this nearby lantern's behavior to that old blocked result.

## Color and mixing

The nearest source reports linear renderer RGB
`(0.058636002, 0.06953986, 0.0815423)` in capture 3299: a modest blue bias, unlike
the saturated blue appearance of the lantern surface in the screenshot. Nearby
orange records include `(1.8577163, 0.5730928, 0.14136323)` at 4.68 game units.
These are renderer values, not measured lumens or exposure-normalized intensities.
The API does not give the visible emissive surface's pixel color.

Live HUD at 10:12:13 CEST: Streaming, 80/124 confirmed clear, 46 in range,
capture 3629 / age 28 ms. All eight calculated channel colors are warm with red
at the command ceiling. Effective gain 1.5, local mix 1, radius 35, angular radius
60 degrees, smoothing 250 ms, neutral color controls. Ambient fill is only
0.000121333 linear; cutoff and extra background are zero. No daylight suppression
is configured. This is calculated output, not measured lamp delivery.

An offline replay uses the existing TelemetryParser and LightMapper, the real
capture, and effective HUD controls. It compares all clear sources against the
same sources with only the nearest one removed. It uses settled local output,
point channel geometry, no Ambient and no live smoothing/wall-region calibration.
For channel 1 the command RGB changes from `(1, .594, .316)` without this source
to `(1, .630, .418)` with it; alone it produces `(.316, .344, .371)`.
Thus the source contributes meaningfully, but the additive result remains warm.
Replay and live HUD are different capture times and must not be called an exact
reconstruction of the live eight-channel output.

The observed problem is loss of recognizable blue color in the mixture, rather
than absence of this source from the consumer. Color fidelity is limited by both
the weak blue bias in the supplied source RGB and overlap with stronger orange
contributions. The investigation does not establish that the source RGB is wrong;
emissive surface color and emitted local-light color can differ.

Raw evidence and offline harness remain ignored under `artifacts/`:
`blue-lamp-20261018-front.json`, `blue-lamp-20261018-confirm-*.json`,
`hue-live-state/blue-lamp-20261018-front.json`, and
`blue-lamp-replay/result-20261018.json`. No production behavior was changed.
Any follow-up mixer change should address source prominence generically and be
checked across scenes; do not hardcode a blue boost, these coordinates or IDs.
