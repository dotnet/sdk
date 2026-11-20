FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble

ENTRYPOINT ["/usr/bin/env"]

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl git python3-pandocfilters \
    && curl --fail --location --retry 3 \
        "https://github.com/jgm/pandoc/releases/download/2.18/pandoc-2.18-1-$(dpkg --print-architecture).deb" \
        --output /tmp/pandoc.deb \
    && apt-get install -y --no-install-recommends /tmp/pandoc.deb \
    && rm /tmp/pandoc.deb \
    && rm -rf /var/lib/apt/lists/*

CMD ["bash", "/manpages/tool/update-man-pages.sh"]
