#!/bin/bash

# GitHub Push Script for DLP Risk Adaptive Protection CSharp
# This script automatically commits and pushes all changes to GitHub

cd "$(dirname "$0")"

echo "📦 Checking git status..."
git status

echo ""
echo "📝 Adding all changes..."
git add .

echo ""
echo "💾 Committing changes..."
TIMESTAMP=$(date +"%Y-%m-%d %H:%M:%S")
git commit -m "Auto-commit: $TIMESTAMP

$(git diff --cached --name-only | sed 's/^/- /')"

echo ""
echo "🚀 Pushing to GitHub..."
git push origin main

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Successfully pushed to GitHub!"
    echo "🔗 Repository: https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp"
else
    echo ""
    echo "❌ Push failed. Please check your authentication."
    echo "💡 Tip: Use Personal Access Token for HTTPS authentication"
    exit 1
fi

