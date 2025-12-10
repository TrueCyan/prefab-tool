#!/bin/bash
# Install prefab-tool git integration
# Run this script from your Unity project root

set -e

echo "=== prefab-tool Git Integration Setup ==="
echo

# Check if prefab-tool is installed
if ! command -v prefab-tool &> /dev/null; then
    echo "Error: prefab-tool is not installed or not in PATH"
    echo "Install it with: pip install prefab-tool"
    exit 1
fi

PREFAB_TOOL_PATH=$(which prefab-tool)
echo "Found prefab-tool at: $PREFAB_TOOL_PATH"

# Setup options
SCOPE="${1:-local}"  # local, global, or pre-commit

# Handle pre-commit setup separately
if [ "$SCOPE" = "pre-commit" ]; then
    echo "Setting up pre-commit hook integration..."

    # Check if we're in a git repo
    if ! git rev-parse --git-dir > /dev/null 2>&1; then
        echo "Error: Not in a git repository"
        exit 1
    fi

    # Check if pre-commit is installed
    if ! command -v pre-commit &> /dev/null; then
        echo "Error: pre-commit is not installed"
        echo "Install it with: pip install pre-commit"
        exit 1
    fi

    # Create .pre-commit-config.yaml if it doesn't exist
    PRECOMMIT_CONFIG=".pre-commit-config.yaml"
    if [ -f "$PRECOMMIT_CONFIG" ]; then
        echo "Found existing $PRECOMMIT_CONFIG"
        if grep -q "prefab-tool" "$PRECOMMIT_CONFIG"; then
            echo "prefab-tool hook already configured"
        else
            echo "Adding prefab-tool hook to existing config..."
            echo "" >> "$PRECOMMIT_CONFIG"
            cat >> "$PRECOMMIT_CONFIG" << 'EOF'
  # Unity Prefab Normalizer
  - repo: https://github.com/TrueCyan/prefab-tool
    rev: v0.1.0
    hooks:
      - id: prefab-normalize
EOF
        fi
    else
        echo "Creating $PRECOMMIT_CONFIG..."
        cat > "$PRECOMMIT_CONFIG" << 'EOF'
# See https://pre-commit.com for more information
repos:
  # Unity Prefab Normalizer
  - repo: https://github.com/TrueCyan/prefab-tool
    rev: v0.1.0
    hooks:
      - id: prefab-normalize
      # Alternative: use prefab-normalize-staged for incremental normalization
      # - id: prefab-normalize-staged
      # Optional: add validation
      # - id: prefab-validate
EOF
    fi

    # Install pre-commit hooks
    echo "Installing pre-commit hooks..."
    pre-commit install

    echo
    echo "=== Pre-commit Setup Complete ==="
    echo
    echo "pre-commit is now configured to normalize Unity files on commit."
    echo
    echo "Available hooks:"
    echo "  - prefab-normalize: Normalize all staged Unity files"
    echo "  - prefab-normalize-staged: Use --changed-only --staged-only"
    echo "  - prefab-validate: Validate Unity files for errors"
    echo
    echo "Test with: pre-commit run --all-files"
    echo
    exit 0
fi

if [ "$SCOPE" = "global" ]; then
    echo "Setting up GLOBAL git configuration..."
    GIT_CONFIG_CMD="git config --global"
else
    echo "Setting up LOCAL git configuration (project only)..."
    GIT_CONFIG_CMD="git config"

    # Check if we're in a git repo
    if ! git rev-parse --git-dir > /dev/null 2>&1; then
        echo "Error: Not in a git repository"
        echo "Run this script from your Unity project root, or use 'install.sh global'"
        exit 1
    fi
fi

# Configure diff driver
echo "Configuring diff driver..."
$GIT_CONFIG_CMD diff.unity.textconv "prefab-tool git-textconv"
$GIT_CONFIG_CMD diff.unity.cachetextconv true

# Configure merge driver
echo "Configuring merge driver..."
$GIT_CONFIG_CMD merge.unity.name "Unity YAML Merge (prefab-tool)"
$GIT_CONFIG_CMD merge.unity.driver "prefab-tool merge %O %A %B -o %A --path %P"
$GIT_CONFIG_CMD merge.unity.recursive binary

echo

# Setup .gitattributes if local
if [ "$SCOPE" = "local" ]; then
    GITATTRIBUTES=".gitattributes"

    # Check if .gitattributes exists
    if [ -f "$GITATTRIBUTES" ]; then
        echo "Found existing .gitattributes"

        # Check if unity diff/merge is already configured
        if grep -q "diff=unity" "$GITATTRIBUTES"; then
            echo "Unity diff already configured in .gitattributes"
        else
            echo "Adding Unity file patterns to .gitattributes..."
            echo "" >> "$GITATTRIBUTES"
            echo "# Unity YAML files - use prefab-tool for diff and merge" >> "$GITATTRIBUTES"
            echo "*.prefab diff=unity merge=unity text eol=lf" >> "$GITATTRIBUTES"
            echo "*.unity diff=unity merge=unity text eol=lf" >> "$GITATTRIBUTES"
            echo "*.asset diff=unity merge=unity text eol=lf" >> "$GITATTRIBUTES"
        fi
    else
        echo "Creating .gitattributes..."
        cat > "$GITATTRIBUTES" << 'EOF'
# Unity YAML files - use prefab-tool for diff and merge
*.prefab diff=unity merge=unity text eol=lf
*.unity diff=unity merge=unity text eol=lf
*.asset diff=unity merge=unity text eol=lf
*.mat diff=unity merge=unity text eol=lf
*.controller diff=unity merge=unity text eol=lf
*.anim diff=unity merge=unity text eol=lf

# Unity meta files
*.meta text eol=lf
EOF
    fi
fi

echo
echo "=== Setup Complete ==="
echo
echo "Git is now configured to use prefab-tool for Unity files."
echo
echo "Benefits:"
echo "  - git diff shows meaningful changes (no serialization noise)"
echo "  - git merge handles Unity files more intelligently"
echo
echo "Test it with:"
echo "  git diff HEAD~1 -- '*.prefab'"
echo
