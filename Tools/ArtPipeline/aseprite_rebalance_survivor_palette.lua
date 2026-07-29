-- Rebalance an approved Project-C survivor animation after Torchstone conform.
--
-- Nearest-palette conform guarantees legal colors, but it cannot enforce the
-- art-direction rule that teal/red are small signals rather than clothing
-- ramps. This pass keeps the editable animation/tags intact while moving the
-- broad teal and warning ramps back to neutral concrete/taupe materials.
-- `teal-item` is deliberately retained as the survivor's one small route mark.

local function required(name)
  local value = app.params[name]
  if value == nil or value == "" then
    error("missing --script-param " .. name .. "=...")
  end
  return value
end

local source = required("source")
local output = required("output")
if not app.fs.isFile(source) then
  error("source does not exist: " .. source)
end

local sprite = Sprite{ fromFile=source }
if sprite == nil then
  error("Aseprite could not open source: " .. source)
end
app.sprite = sprite
if sprite.colorMode ~= ColorMode.RGB then
  app.command.ChangePixelFormat{ format="rgb" }
end

local function key(red, green, blue)
  return red * 65536 + green * 256 + blue
end

local remap = {
  -- Broad teal clothing ramps -> neutral concrete/charcoal ramps.
  [key(55, 106, 103)] = { 107, 113, 120 }, -- teal-mid -> concrete
  [key(79, 167, 160)] = { 151, 144, 126 }, -- pc-teal -> pc-dim
  [key(20, 52, 58)] = { 31, 31, 27 },      -- pc-teal-bg -> ash
  [key(28, 67, 71)] = { 59, 63, 69 },      -- water -> concrete-dim
  [key(154, 223, 232)] = { 207, 192, 174 },-- pc-ice -> pc-stone-lit
  [key(198, 244, 247)] = { 234, 223, 200 },-- ice-lit -> pc-text

  -- Saturated warning clothing -> weathered rust material ramps.
  [key(69, 16, 11)] = { 90, 46, 27 },      -- pc-hp-empty -> rust-dark
  [key(164, 49, 34)] = { 122, 62, 28 },    -- red-dark -> rust-brown
  [key(216, 69, 42)] = { 156, 90, 46 },    -- pc-hp -> rust
  [key(240, 73, 42)] = { 156, 90, 46 },    -- pc-warning -> rust
}

local changed = 0
app.transaction("Project-C survivor palette rebalance", function()
  for _, cel in ipairs(sprite.cels) do
    for pixel in cel.image:pixels() do
      local value = pixel()
      local alpha = app.pixelColor.rgbaA(value)
      if alpha > 0 then
        local replacement = remap[key(
          app.pixelColor.rgbaR(value),
          app.pixelColor.rgbaG(value),
          app.pixelColor.rgbaB(value)
        )]
        if replacement ~= nil then
          pixel(app.pixelColor.rgba(
            replacement[1],
            replacement[2],
            replacement[3],
            alpha
          ))
          changed = changed + 1
        end
      end
    end
  end
end)

sprite:saveAs(output)
sprite:close()
print(
  "rebalanced survivor palette " .. source .. " -> " .. output ..
  " (changed=" .. tostring(changed) .. ")"
)
