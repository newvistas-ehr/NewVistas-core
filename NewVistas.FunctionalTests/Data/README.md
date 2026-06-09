# Synthetic DICOM test fixtures

`ct.dcm` and `mr.dcm` are tiny, **synthetic** DICOM images used by
`ImagingStoragePipelineTests` to exercise the imaging ingestion pipeline
(parse → render thumbnail → blob write → grain write).

- **Not real patient data.** Generated programmatically with fo-dicom: a 64×64
  16-bit `MONOCHROME2` gradient with valid Study/Series/Instance UIDs, an
  Explicit-VR-Little-Endian transfer syntax, window center/width, and `Modality`
  set to `CT` / `MR` respectively — which is everything the tests assert.
- **No third-party rights.** Authored for this repository; free to use, modify,
  and redistribute. They replace fixtures that previously came from the vendored
  fo-dicom source tree (since removed from the repo).

To regenerate: a small fo-dicom console program builds a `DicomDataset` with the
tags above, writes 64×64×16-bit pixel data via `DicomPixelData.AddFrame`, and
saves each file. The tests only need the committed `.dcm` outputs.
