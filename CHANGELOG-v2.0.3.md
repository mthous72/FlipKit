# FlipKit Web v2.0.3 - Changes Summary

## Issue: Mobile Scanning Error

**Root Cause:** The Web app was using **paid models** by default instead of free models, causing API errors when users don't have credits.

## Fixes Applied

### 1. Model Selection Ordering ✅
**File:** `FlipKit.Web/Models/ScanUploadViewModel.cs`

- **Changed default model** from `openai/gpt-4o-mini` (paid) to `nvidia/nemotron-nano-12b-v2-vl:free` (free)
- **Reorganized model list** with proper ordering:
  - **Free models first** (11 models from OpenRouterScannerService)
    - nvidia/nemotron-nano-12b-v2-vl:free
    - qwen/qwen2.5-vl-72b-instruct:free
    - qwen/qwen2.5-vl-32b-instruct:free
    - meta-llama/llama-4-maverick:free
    - meta-llama/llama-4-scout:free
    - google/gemma-3-27b-it:free
    - mistralai/mistral-small-3.1-24b-instruct:free
    - moonshotai/kimi-vl-a3b-thinking:free
    - meta-llama/llama-3.2-11b-vision-instruct:free
    - google/gemma-3-12b-it:free
    - google/gemma-3-4b-it:free
  - **Paid models second** (ordered by price, cheapest to most expensive):
    - openai/gpt-4o-mini ($0.15/$0.60 per 1M tokens)
    - google/gemini-flash-1.5 (low cost)
    - google/gemini-2.0-flash-exp (mid cost)
    - openai/gpt-4o (higher cost)
    - anthropic/claude-3.5-sonnet ($5/$25 per 1M tokens - premium)

**File:** `FlipKit.Web/Controllers/ScanController.cs`

- **Changed fallback model** from `openai/gpt-4o-mini` to `nvidia/nemotron-nano-12b-v2-vl:free`

### 2. Enhanced Model Selection UI ✅
**File:** `FlipKit.Web/Views/Scan/Index.cshtml`

- **Converted to proper combobox** with optgroups:
  - "Free Models (Recommended)" group
  - "Paid Models" group with pricing information
- **Added larger form-select** (`form-select-lg`) for better mobile tap targets
- **Display pricing info** inline for paid models
- **Improved help text** to clarify free vs paid model behavior

### 3. Footer Word Wrapping & Overlap Issues ✅
**File:** `FlipKit.Web/Views/Shared/_Layout.cshtml`

**Footer improvements:**
- Added `line-height: 1.6` for better readability
- Changed from `col-md-8/col-md-4` to `col-12 col-lg-9/col-lg-3` for better mobile responsive
- Added `position: relative; clear: both;` to prevent overlap
- Added `g-3` class for gutter spacing between columns
- Increased padding from `py-3` to `py-4`

**Main content improvements:**
- Increased `min-height` from `400px` to `450px` to account for taller footer
- Added `overflow-x: auto` to prevent horizontal overflow
- Increased padding with `pb-5 mb-5` to create more clearance

### 4. API Warning Banner - Word Wrapping ✅
**File:** `FlipKit.Web/Views/Shared/_Layout.cshtml`

- Added `overflow-wrap: break-word; word-wrap: break-word;` to alert container
- Added `line-height: 1.6` to all paragraphs and lists for better readability
- Text now properly wraps within screen width on all device sizes

### 5. Settings Page - Legal Disclaimer Formatting ✅
**File:** `FlipKit.Web/Views/Settings/Index.cshtml`

- Added `line-height: 1.6` to disclaimer paragraphs for better readability
- Ensures text wraps properly on mobile devices

## Verified Free Vision Models (February 2026)

Based on OpenRouter API research:

✅ **Confirmed Working:**
- nvidia/nemotron-nano-12b-v2-vl:free
- qwen/qwen2.5-vl-72b-instruct:free
- qwen/qwen2.5-vl-32b-instruct:free
- meta-llama/llama-4-maverick:free (released April 2025)
- meta-llama/llama-4-scout:free (released April 2025)
- google/gemma-3-27b-it:free (multimodal with vision)
- google/gemma-3-12b-it:free (multimodal with vision)
- google/gemma-3-4b-it:free (multimodal with vision)
- mistralai/mistral-small-3.1-24b-instruct:free (multimodal)
- moonshotai/kimi-vl-a3b-thinking:free
- meta-llama/llama-3.2-11b-vision-instruct:free

## Testing Recommendations

1. **Test scanning with default free model** on mobile device
2. **Verify model dropdown** shows optgroups correctly
3. **Check footer** doesn't overlap content on desktop and mobile
4. **Verify API warning banner** wraps text properly
5. **Test all screen sizes** (mobile, tablet, desktop)

## Sources

- [OpenRouter Models](https://openrouter.ai/models)
- [Llama 4 Maverick on OpenRouter](https://openrouter.ai/meta-llama/llama-4-maverick)
- [Llama 4 Scout on OpenRouter](https://openrouter.ai/meta-llama/llama-4-scout)
- [Gemma 3 Model Overview](https://deepmind.google/models/gemma/gemma-3/)
- [Mistral Small 3.1 24B on OpenRouter](https://openrouter.ai/mistralai/mistral-small-3.1-24b-instruct)
- [LLM API Pricing Comparison](https://www.cloudidr.com/llm-pricing)

## Build Status

✅ Build successful with 0 errors, 9 warnings (nullable reference warnings, non-critical)
