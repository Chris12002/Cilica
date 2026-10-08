FROM sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80
COPY cilica/ /approved/cilica/
COPY offline-feed/ /approved/offline-feed/
COPY input/ /approved/input/
COPY input-provenance.json /approved/input-provenance.json
COPY runner.py /runner.py
USER 10001:10001
ENTRYPOINT ["python3", "-B", "/runner.py"]
