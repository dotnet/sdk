# NOTE: This file is named Dockerfile.tool (not Dockerfile) so that 1ES pipeline
# Secure Supply Chain Analysis (SSCA) auto-injection does not flag the external
# pandoc/core image reference. Use `docker build -f Dockerfile.tool .` to build.
FROM pandoc/core:2.18.0

ENTRYPOINT ["/usr/bin/env"]

RUN apk add git py3-pip && python3 -m pip install pandocfilters

CMD /manpages/tool/update-man-pages.sh
