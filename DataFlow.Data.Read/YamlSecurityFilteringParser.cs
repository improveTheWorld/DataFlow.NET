using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace DataFlow;

/// <summary>
/// Streaming security wrapper enforcing YAML guard rails without full buffering.
/// Generic on T only because the underlying options type is generic.
/// </summary>
internal sealed class SecurityFilteringParser<T> : IParser
{
    private readonly IParser _inner;
    private readonly YamlReadOptions<T> _options;

    private ParsingEvent? _current;
    private bool _advanced;
    private bool _finished;

    private int _depth = 0;
    private long _documents = 0;
    private bool _sequenceRootDetermined = false;
    private bool _sequenceRootMode = false;

    // Core YAML 1.2 tag whitelist (canonical + !! short forms)
    private static readonly HashSet<string> CoreTags = new()
    {
        "!!str",
        "tag:yaml.org,2002:str",
        "!!int",
        "tag:yaml.org,2002:int",
        "!!bool",
        "tag:yaml.org,2002:bool",
        "!!null",
        "tag:yaml.org,2002:null",
        "!!float",
        "tag:yaml.org,2002:float",
        "!!seq",
        "tag:yaml.org,2002:seq",
        "!!map",
        "tag:yaml.org,2002:map",
        "!!timestamp",
        "tag:yaml.org,2002:timestamp",
        "!!binary",
        "tag:yaml.org,2002:binary"
    };

    public SecurityFilteringParser(IParser inner, YamlReadOptions<T> options)
    {
        _inner = inner;
        _options = options;
    }

    public ParsingEvent Current => _current!;

    public bool Accept<TEvent>(out TEvent @event) where TEvent : ParsingEvent
    {
        // Ensure we have at least one current event, but do NOT advance further.
        if (!_advanced) EnsureAdvanced();
        if (_current is TEvent matched)
        {
            @event = matched;
            return true;
        }
        @event = null!;
        return false;
    }

    public bool MoveNext()
    {
        if (_finished) return false;
        // Mark that we need to advance to the next event
        _advanced = false;
        EnsureAdvanced();          // This will advance underlying parser to next acceptable event
        return !_finished;
    }


    private void EnsureAdvanced()
    {
        if (_advanced) return;

        while (true)
        {
            if (!_inner.MoveNext())
            {
                _finished = true;
                _current = new StreamEnd();
                _advanced = true;
                return;
            }

            _current = _inner.Current;
            if (_current is StreamEnd)
            {
                _finished = true;
                _advanced = true;
                return;
            }

            // Determine root mode once
            if (!_sequenceRootDetermined)
            {
                if (_current is SequenceStart)
                {
                    _sequenceRootMode = true;
                    _sequenceRootDetermined = true;
                }
                else if (_current is DocumentStart)
                {
                    _sequenceRootMode = false;
                    _sequenceRootDetermined = true;
                }
            }

            // Document / element counting
            if (_current is DocumentStart)
            {
                _documents++;
                if (_options.MaxTotalDocuments > 0 && _documents > _options.MaxTotalDocuments)
                {
                    if (!EmitYamlSecurityError("Too many documents", $"MaxTotalDocuments={_options.MaxTotalDocuments}"))
                    {
                        _finished = true;
                        _current = new StreamEnd();
                        _advanced = true;
                        return;
                    }
                    SkipDocument(_inner);
                    continue;
                }
            }
            else if (_sequenceRootMode &&
                     (_current is MappingStart or SequenceStart or Scalar) &&
                     _depth == 0) // top-level element
            {
                _documents++;
                if (_options.MaxTotalDocuments > 0 && _documents > _options.MaxTotalDocuments)
                {
                    if (!EmitYamlSecurityError("Too many elements", $"MaxTotalDocuments={_options.MaxTotalDocuments}"))
                    {
                        _finished = true;
                        _current = new StreamEnd();
                        _advanced = true;
                        return;
                    }
                    SkipSequenceElement(_inner);
                    continue;
                }
            }

            // Depth tracking
            if (_current is SequenceStart or MappingStart)
            {
                _depth++;
                if (_options.MaxDepth > 0 && _depth > _options.MaxDepth)
                {
                    if (!EmitYamlSecurityError("MaxDepth exceeded", $"Depth={_depth} Max={_options.MaxDepth}"))
                    {
                        _finished = true;
                        _current = new StreamEnd();
                        _advanced = true;
                        return;
                    }
                    SkipContainer(_inner);
                    _depth--; // adjust after skip
                    continue;
                }
            }
            else if (_current is SequenceEnd or MappingEnd)
            {
                _depth = _depth <= 0 ? 0 : _depth - 1;
            }

            // Alias / Anchor enforcement
            if (_options.DisallowAliases)
            {
                if (_current is AnchorAlias alias)
                {
                    //  AnchorAlias.Value is AnchorName; convert to string
                    var aliasName = alias.Value.ToString();
                    if (!EmitYamlSecurityError("Alias usage disallowed", aliasName))
                    {
                        _finished = true;
                        _current = new StreamEnd();
                        _advanced = true;
                        return;
                    }
                    continue; // skip alias
                }
                if (_current is NodeEvent ne && !ne.Anchor.IsEmpty)
                {
                    var anchorName = ne.Anchor.Value; // AnchorName.Value is string
                    if (!EmitYamlSecurityError("Anchor definition disallowed", anchorName))
                    {
                        _finished = true;
                        _current = new StreamEnd();
                        _advanced = true;
                        return;
                    }
                    if (_current is Scalar)
                        continue;
                    if (_current is MappingStart or SequenceStart)
                    {
                        SkipContainer(_inner);
                        continue;
                    }
                }
            }

            // Tag enforcement
            if (_options.DisallowCustomTags &&
                _current is NodeEvent nodeEv &&
                !nodeEv.Tag.IsEmpty)
            {
                var tagValue = nodeEv.Tag.Value;
                if (!IsAllowedTag(tagValue))
                {
                    if (!EmitYamlSecurityError("Custom tag disallowed", tagValue))
                    {
                        _finished = true;
                        _current = new StreamEnd();
                        _advanced = true;
                        return;
                    }
                    if (nodeEv is Scalar)
                        continue;
                    if (nodeEv is MappingStart or SequenceStart)
                    {
                        SkipContainer(_inner);
                        continue;
                    }
                }
            }

            // Scalar length
            if (_options.MaxNodeScalarLength > 0 && _current is Scalar sc)
            {
                if (sc.Value != null && sc.Value.Length > _options.MaxNodeScalarLength)
                {
                    if (!EmitYamlSecurityError("Scalar length exceeded",
                            $"Len={sc.Value.Length} Max={_options.MaxNodeScalarLength}"))
                    {
                        _finished = true;
                        _current = new StreamEnd();
                        _advanced = true;
                        return;
                    }
                    // BUG-007 FIX: Throw InvalidDataException instead of continue.
                    // The previous 'continue' caused an infinite loop because:
                    // 1. The scalar is part of a mapping (e.g., "name: XXXX...")
                    // 2. Just skipping the scalar leaves the mapping incomplete
                    // 3. The parser stays stuck trying to read the incomplete mapping
                    // By throwing, the outer Read.Yaml catch-resync logic properly
                    // skips the entire element and continues with the next one.
                    throw new System.IO.InvalidDataException(
                        $"YAML scalar exceeds MaxNodeScalarLength: Len={sc.Value.Length} Max={_options.MaxNodeScalarLength}");
                }
            }

            _advanced = true;
            return;
        }
    }

    private bool EmitYamlSecurityError(string msg, string detail)
    {
        return _options.HandleError("YAML", -1, _documents, _options.FilePath ?? "",
            "YamlSecurityError", msg, detail);
    }

    private static bool IsAllowedTag(string tag)
    {
        if (CoreTags.Contains(tag)) return true;
        // Accept both !!short and full canonical; normalize simple case
        if (tag.StartsWith("tag:yaml.org,2002:"))
        {
            var shortForm = "!!" + tag.Substring("tag:yaml.org,2002:".Length);
            if (CoreTags.Contains(shortForm)) return true;
        }
        if (tag.StartsWith("!!"))
        {
            var canonical = "tag:yaml.org,2002:" + tag.Substring(2);
            if (CoreTags.Contains(canonical)) return true;
        }
        return false;
    }

    // Public skip helpers (used externally by Read.Yaml catch logic)

    internal static void SkipDocument(IParser parser)
    {
        int depth = 0;
        while (parser.MoveNext())
        {
            var ev = parser.Current;
            if (ev is DocumentEnd) break;
            if (ev is SequenceStart or MappingStart) depth++;
            else if (ev is SequenceEnd or MappingEnd) depth--;
        }
    }

    internal static void SkipSequenceElement(IParser parser)
    {
        int depth = 0;
        bool started = false;
        while (parser.MoveNext())
        {
            var ev = parser.Current;
            if (!started)
            {
                // first event of element already consumed by caller; mark started
                started = true;
            }

            if (ev is SequenceStart or MappingStart) depth++;
            else if (ev is SequenceEnd or MappingEnd)
            {
                if (depth == 0) break;
                depth--;
            }
            // scalar ends immediately (depth == 0) -> break
            if (depth == 0 && ev is Scalar) break;
        }
    }

    private static void SkipContainer(IParser parser)
    {
        int depth = 0;
        while (parser.MoveNext())
        {
            var ev = parser.Current;
            if (ev is SequenceStart or MappingStart) depth++;
            else if (ev is SequenceEnd or MappingEnd)
            {
                if (depth == 0) break;
                depth--;
            }
        }
    }
    internal static void ResyncFailedSequenceElement(IParser parser)
    {
        // We are somewhere inside a top-level element (typically a mapping) whose
        // MappingStart was already consumed. We need to consume until we reach the
        // matching MappingEnd (depth balanced) at top level (depth == 0) and then
        // advance once to land on the next element's first event (or SequenceEnd).
        int depth = 0;
        // We do NOT call MoveNext first; parser.Current is whatever the
        // deserializer left it at (often the scalar key or its value). We want to
        // move forward from here.
        while (parser.MoveNext())
        {
            var ev = parser.Current;
            if (ev is MappingStart or SequenceStart)
            {
                depth++;
            }
            else if (ev is MappingEnd or SequenceEnd)
            {
                if (depth == 0)
                {
                    // We reached the end of the failed element's root container
                    // Advance once more so outer loop starts with next element (or SequenceEnd)
                    parser.MoveNext();
                    break;
                }
                depth--;
            }
        }
    }
}
