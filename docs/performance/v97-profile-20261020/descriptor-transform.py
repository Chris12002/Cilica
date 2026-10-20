import re
def types(s):
 sizes={m[2]:int(m[1]) for m in re.finditer(r'static constexpr std::array<const Descriptor\*, (\d+)> (\w+)_types =',s)}
 pattern=r'    for \(const auto\* receiver : (\w+)_types\) if \(receiver == type\) return true;\n    return false;'
 def indexed(m):
  n=sizes[m[1]]
  if n<8:return m[0]
  cap=1
  while cap<n*2:cap*=2
  return f'''    if (type == nullptr) return false;
    static const auto slots = [] {{
        std::array<const Descriptor*, {cap}> result{{}};
        for (auto* p : {m[1]}_types) {{
            auto a = reinterpret_cast<std::uintptr_t>(p);
            std::size_t i = ((a >> 3) ^ (a >> 17)) & {cap-1};
            while (result[i] != nullptr) i = (i + 1) & {cap-1};
            result[i] = p;
        }}
        return result;
    }}();
    auto a = reinterpret_cast<std::uintptr_t>(type);
    std::size_t i = ((a >> 3) ^ (a >> 17)) & {cap-1};
    while (slots[i] != nullptr) {{ if (slots[i] == type) return true; i = (i + 1) & {cap-1}; }}
    return false;'''
 s,n=re.subn(pattern,indexed,s);assert n>10
 return s
