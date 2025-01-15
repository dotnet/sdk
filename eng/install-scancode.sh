#!/bin/bash

set -euo pipefail

# Install instructions: https://scancode-toolkit.readthedocs.io/en/latest/getting-started/install.html#installation-as-a-library-via-pip

# See latest release at https://github.com/nexB/scancode-toolkit/releases
SCANCODE_VERSION="32.3.1"

pyEnvPath="/tmp/scancode-env"
python3 -m venv $pyEnvPath
source $pyEnvPath/bin/activate
pip install scancode-toolkit==$SCANCODE_VERSION
deactivate

# Setup a script which executes scancode in the virtual environment
cat > /usr/local/bin/scancode << EOF
#!/bin/bash
set -euo pipefail
source $pyEnvPath/bin/activate
scancode "\$@"
deactivate
EOF

chmod +x /usr/local/bin/scancode
