-- Project-C static asset conformer.
--
-- Opens one generated PNG, verifies/resizes its canvas, maps every visible
-- pixel to the shared Torchstone palette without dithering, and saves an
-- editable .aseprite source. RGBA is intentionally retained: palette index 0
-- is the opaque pc-void color, so using it as Indexed transparency would make
-- legitimate void pixels disappear.

local function required(name)
  local value = app.params[name]
  if value == nil or value == "" then
    error("missing --script-param " .. name .. "=...")
  end
  return value
end

local function integerParam(name, defaultValue)
  local raw = app.params[name]
  if raw == nil or raw == "" then
    return defaultValue
  end
  local value = tonumber(raw)
  if value == nil or value % 1 ~= 0 then
    error(name .. " must be an integer")
  end
  return value
end

local source = required("source")
local output = required("output")
local palettePath = required("palette")
local expectedWidth = integerParam("width", 0)
local expectedHeight = integerParam("height", 0)
local alphaCutoff = integerParam("alpha_cutoff", 80)
local resize = app.params["resize"] or "strict"

if not app.fs.isFile(source) then
  error("source does not exist: " .. source)
end
if not app.fs.isFile(palettePath) then
  error("palette does not exist: " .. palettePath)
end
if not string.match(string.lower(output), "%.aseprite$") and
   not string.match(string.lower(output), "%.ase$") then
  error("output must end in .aseprite or .ase: " .. output)
end
if alphaCutoff < 0 or alphaCutoff > 255 then
  error("alpha_cutoff must be in 0..255")
end

local sprite = Sprite{ fromFile=source, oneFrame=true }
if sprite == nil then
  error("Aseprite could not open source: " .. source)
end
app.sprite = sprite

if expectedWidth > 0 and expectedHeight > 0 and
   (sprite.width ~= expectedWidth or sprite.height ~= expectedHeight) then
  if resize == "nearest" then
    app.command.SpriteSize{
      ui=false,
      width=expectedWidth,
      height=expectedHeight,
      lockRatio=false,
      method="nearest"
    }
  else
    local actual = tostring(sprite.width) .. "x" .. tostring(sprite.height)
    sprite:close()
    error(
      "canvas mismatch: expected " ..
      tostring(expectedWidth) .. "x" .. tostring(expectedHeight) ..
      ", got " .. actual ..
      " (pass resize=nearest only for an intentional exact resize)"
    )
  end
end

if sprite.colorMode ~= ColorMode.RGB then
  app.command.ChangePixelFormat{ format="rgb" }
end

local palette = Palette{ fromFile=palettePath }
if palette == nil or #palette == 0 then
  sprite:close()
  error("could not load palette: " .. palettePath)
end
sprite:setPalette(palette)

local colors = {}
for index = 0, #palette - 1 do
  local color = palette:getColor(index)
  colors[#colors + 1] = {
    red=color.red,
    green=color.green,
    blue=color.blue
  }
end

local cache = {}
local mappedPixels = 0
local transparentPixels = 0

local function nearestColor(red, green, blue)
  local key = red * 65536 + green * 256 + blue
  local cached = cache[key]
  if cached ~= nil then
    return cached
  end

  local best = colors[1]
  local bestDistance = math.huge
  for _, candidate in ipairs(colors) do
    local dr = red - candidate.red
    local dg = green - candidate.green
    local db = blue - candidate.blue
    local distance = dr * dr + dg * dg + db * db
    if distance < bestDistance then
      best = candidate
      bestDistance = distance
    end
  end
  cache[key] = best
  return best
end

app.transaction("Project-C palette conform", function()
  for _, cel in ipairs(sprite.cels) do
    local image = cel.image
    for pixel in image:pixels() do
      local value = pixel()
      local alpha = app.pixelColor.rgbaA(value)
      if alpha < alphaCutoff then
        pixel(app.pixelColor.rgba(0, 0, 0, 0))
        transparentPixels = transparentPixels + 1
      else
        local mapped = nearestColor(
          app.pixelColor.rgbaR(value),
          app.pixelColor.rgbaG(value),
          app.pixelColor.rgbaB(value)
        )
        pixel(app.pixelColor.rgba(mapped.red, mapped.green, mapped.blue, 255))
        mappedPixels = mappedPixels + 1
      end
    end
  end
end)

if #sprite.layers == 1 then
  sprite.layers[1].name = "base"
end
sprite:saveAs(output)
sprite:close()

print(
  "conformed " .. source .. " -> " .. output ..
  " (" .. tostring(expectedWidth) .. "x" .. tostring(expectedHeight) ..
  ", mapped=" .. tostring(mappedPixels) ..
  ", transparent=" .. tostring(transparentPixels) .. ")"
)
