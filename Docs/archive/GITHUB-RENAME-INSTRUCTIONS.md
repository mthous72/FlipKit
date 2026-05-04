# GitHub Repository Rename Instructions

## Manual Steps Required

The GitHub repository needs to be renamed from `CardLister` to `FlipKit`. This must be done manually through the GitHub web interface.

---

## Step-by-Step Instructions

### 1. Rename the Repository

1. Go to https://github.com/mthous72/CardLister
2. Click **Settings** (in the top navigation)
3. Scroll down to the **Repository name** section
4. Change the name from `CardLister` to `FlipKit`
5. Click **Rename**

**Important:** GitHub will automatically set up redirects from the old URL to the new URL, so existing links won't break.

### 2. Update Repository Description

While in Settings, update the description:

**Old:** "Sports card inventory and pricing tool for sellers"

**New:** "FlipKit - Sports card inventory and pricing tool for sellers. AI-powered scanning, pricing research, and sales tracking."

### 3. Update Repository Topics/Tags

In the main repository page:

1. Click the ⚙️ icon next to "About"
2. Update topics to: `sports-cards`, `inventory-management`, `pricing-tool`, `card-seller`, `whatnot`, `ebay`, `avalonia`, `aspnet-core`, `dotnet`
3. Save changes

### 4. Update Your Local Repository

After renaming on GitHub, update your local repository:

```bash
# Update the remote URL
git remote set-url origin https://github.com/mthous72/FlipKit.git

# Verify the new URL
git remote -v
```

### 5. Update README Badges (if any)

If your README.md has any badges with the old repository name, they'll automatically redirect but you can update them for clarity.

### 6. Update Social Media / External Links

If you've shared the CardLister repository anywhere (Twitter, Reddit, forums, etc.), consider posting an update:

> "CardLister has been rebranded to FlipKit! 🎉 Same great features, better name. Check it out: https://github.com/mthous72/FlipKit"

---

## What GitHub Handles Automatically

✅ **URL Redirects** - Old URLs automatically redirect to new URLs
✅ **Clone URLs** - Both old and new URLs work
✅ **Issue/PR Links** - All existing links remain functional
✅ **Git Remotes** - Existing clones continue to work (but update recommended)

---

## What You Need to Update Manually

❌ **Repository Name** - Manual rename required (see above)
❌ **Description** - Update in Settings → About
❌ **Topics** - Update in main page → About
❌ **External Links** - Any links you've shared on social media, forums, etc.
❌ **Documentation** - Already done in v3.0.0 (all docs updated)

---

## Verification Checklist

After renaming:

- [ ] Repository accessible at https://github.com/mthous72/FlipKit
- [ ] Old URL redirects to new URL
- [ ] Description updated
- [ ] Topics/tags updated
- [ ] Local git remote updated
- [ ] Can clone with new URL: `git clone https://github.com/mthous72/FlipKit.git`
- [ ] README displays correctly
- [ ] v3.0.0 release visible

---

## Notes

- **Rename is safe** - GitHub preserves all history, issues, PRs, and releases
- **No downtime** - The repository remains accessible during rename
- **Reversible** - You can rename back if needed (though not recommended)
- **One-time action** - Once renamed, you're done!

---

## Need Help?

See GitHub's official documentation: https://docs.github.com/en/repositories/creating-and-managing-repositories/renaming-a-repository
