# VirusTotal check for the final OPK

Overwolf states that an OPK with antivirus warnings will not be tested. Perform
this check only after the final OPK has been rebuilt; any later byte change
invalidates the report and SHA-256.

1. Open `https://www.virustotal.com/gui/home/upload`.
2. Upload `Warframe-Tracker-Native-0.1.2.opk` from the submission folder.
3. Wait until all engines finish.
4. The required result is `0` detections. If any engine flags it, do not submit;
   save the detection name and investigate first.
5. Copy the final report URL into `NATIVE_QA_RESULTS.md`.
6. Save a screenshot as `reports/virustotal-0-detections.png`.
7. Recalculate SHA-256 and confirm it matches `CHECKSUMS-SHA256.txt`.

The OPK contains HTML, JavaScript, CSS, images and `manifest.json`; it does not
contain an EXE, DLL, development key, Warframe credential or database password.
Never advise reviewers to disable Defender, SmartScreen or antivirus software.
