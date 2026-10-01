import os
import base64
from io import BytesIO
from PIL import Image, ImageDraw

output_dir = os.path.dirname(os.path.abspath(__file__))
os.makedirs(output_dir, exist_ok=True)

# Paleta refinada Buraqueira
P = {
    '.': (0, 0, 0, 0),             # Transparente
    '#': (32, 18, 10, 255),        # Contorno externo / sombra profunda
    'D': (65, 38, 22, 255),        # Marrom profundo
    'B': (110, 68, 36, 255),       # Marrom principal
    'b': (145, 92, 52, 255),       # Marrom médio
    'O': (180, 122, 70, 255),      # Ocre / terra
    'o': (215, 160, 105, 255),     # Ocre claro
    'C': (242, 232, 212, 255),     # Creme / penugem clara
    'W': (255, 255, 255, 255),     # Branco puro
    'Y': (255, 215, 0, 255),       # Amarelo ouro
    'y': (220, 165, 5, 255),       # Amarelo sombra
    'K': (18, 18, 20, 255),        # Preto pupila
    'A': (245, 135, 15, 255),      # Âmbar do bico / garras
    'a': (195, 95, 10, 255),       # Âmbar escuro
    # Cores do Mapinha Urbano
    'M_PAPER': (248, 250, 252, 255),  # Papel branco azulado
    'M_SHADOW': (218, 228, 238, 255), # Dobra sombreada
    'M_GRID': (160, 175, 195, 255),   # Ruas / malha cinza
    'M_ROAD': (245, 158, 11, 255),    # Avenida amarela/laranja principal
    'M_PIN': (239, 68, 68, 255),      # Pino vermelho GPS
    'M_WATER': (56, 189, 248, 255),   # Rio / água azul
    'M_BORDER': (71, 85, 105, 255),   # Borda do mapa
}

def generate_buraqueira_urb_48():
    img = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 1. Contorno e cabeça da Coruja (x: 10 a 37, y: 1 a 22)
    draw.ellipse([10, 1, 37, 22], fill=P['#'])
    draw.ellipse([11, 2, 36, 21], fill=P['b'])

    # Penugem da testa mosqueada
    for y in range(3, 8):
        for x in range(13, 35, 2):
            if (x + y) % 3 == 0:
                draw.point((x, y), fill=P['D'])
            elif (x + y) % 5 == 0:
                draw.point((x, y), fill=P['C'])

    # Discos faciais
    draw.ellipse([11, 7, 24, 20], fill=P['o'])
    draw.ellipse([23, 7, 36, 20], fill=P['o'])

    # Sobrancelhas brancas marcantes em 'V'
    draw.line([12, 7, 20, 8], fill=P['W'], width=2)
    draw.line([21, 8, 23, 10], fill=P['W'], width=2)
    draw.line([24, 10, 26, 8], fill=P['W'], width=2)
    draw.line([27, 8, 35, 7], fill=P['W'], width=2)

    # Olho Esquerdo: centro (17, 13), raio 4
    draw.ellipse([13, 9, 21, 17], fill=P['#'])
    draw.ellipse([14, 10, 20, 16], fill=P['Y'])
    draw.ellipse([15, 11, 19, 15], fill=P['K'])
    draw.point((16, 11), fill=P['W'])
    draw.point((17, 11), fill=P['W'])

    # Olho Direito: centro (30, 13), raio 4
    draw.ellipse([26, 9, 34, 17], fill=P['#'])
    draw.ellipse([27, 10, 33, 16], fill=P['Y'])
    draw.ellipse([28, 11, 32, 15], fill=P['K'])
    draw.point((29, 11), fill=P['W'])
    draw.point((30, 11), fill=P['W'])

    # Bico
    draw.polygon([(22, 12), (25, 12), (24, 20), (23, 20)], fill=P['A'])
    draw.polygon([(24, 13), (25, 13), (24, 20)], fill=P['a'])

    # Corpo / Asas (y: 19 a 36, x: 8 a 39)
    draw.ellipse([8, 18, 39, 37], fill=P['#'])
    draw.ellipse([9, 19, 38, 36], fill=P['D'])
    draw.ellipse([9, 19, 17, 35], fill=P['b'])
    draw.ellipse([30, 19, 38, 35], fill=P['b'])

    # Peito
    draw.ellipse([15, 19, 32, 35], fill=P['C'])
    for y in range(20, 32, 2):
        for x in range(16, 32, 2):
            if (x + y) % 3 == 0:
                draw.point((x, y), fill=P['B'])

    # 2. O MAPINHA DOBRADO / PLANO URBANO NA FRENTE (y: 27 a 46, x: 4 a 43)
    # Sombra projetada do mapa
    draw.polygon([(3, 44), (44, 44), (42, 47), (5, 47)], fill=(20, 20, 25, 120))

    # Folha central do mapa
    draw.polygon([(16, 27), (31, 28), (31, 44), (16, 43)], fill=P['M_PAPER'])
    # Folha esquerda
    draw.polygon([(4, 29), (16, 27), (16, 43), (4, 45)], fill=P['M_SHADOW'])
    # Folha direita
    draw.polygon([(31, 28), (43, 29), (43, 46), (31, 44)], fill=P['M_SHADOW'])

    # Bordas externas do mapa
    draw.line([(4, 29), (16, 27), (31, 28), (43, 29), (43, 46), (31, 44), (16, 43), (4, 45), (4, 29)], fill=P['M_BORDER'], width=1)
    # Linhas de dobra
    draw.line([(16, 27), (16, 43)], fill=P['M_GRID'], width=1)
    draw.line([(31, 28), (31, 44)], fill=P['M_GRID'], width=1)

    # Rio azul na dobra esquerda
    draw.line([(6, 33), (10, 36), (14, 38)], fill=P['M_WATER'], width=2)
    # Malha viária cinza
    draw.line([(18, 32), (29, 33)], fill=P['M_GRID'], width=1)
    draw.line([(18, 38), (29, 39)], fill=P['M_GRID'], width=1)
    draw.line([(22, 29), (22, 42)], fill=P['M_GRID'], width=1)
    draw.line([(27, 30), (27, 43)], fill=P['M_GRID'], width=1)
    draw.line([(33, 34), (41, 35)], fill=P['M_GRID'], width=1)
    draw.line([(33, 40), (41, 41)], fill=P['M_GRID'], width=1)

    # Avenida principal em destaque (laranja)
    draw.line([(7, 41), (16, 35), (31, 35), (41, 38)], fill=P['M_ROAD'], width=2)

    # Pino de localização vermelho GPS no centro
    draw.ellipse([22, 33, 26, 37], fill=P['M_PIN'])
    draw.polygon([(23, 36), (25, 36), (24, 39)], fill=P['M_PIN'])
    draw.point((24, 34), fill=P['W'])

    # Garrinhas da coruja segurando a borda superior do mapa
    draw.rectangle([13, 25, 15, 28], fill=P['A'])
    draw.rectangle([17, 25, 19, 28], fill=P['A'])
    draw.point((14, 28), fill=P['K'])
    draw.point((18, 28), fill=P['K'])

    draw.rectangle([28, 25, 30, 28], fill=P['A'])
    draw.rectangle([32, 25, 34, 28], fill=P['A'])
    draw.point((29, 28), fill=P['K'])
    draw.point((33, 28), fill=P['K'])

    return img

def generate_buraqueira_urb_24():
    img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 1. Cabeça da coruja
    draw.ellipse([4, 1, 19, 13], fill=P['#'])
    draw.ellipse([5, 2, 18, 12], fill=P['b'])

    # Sobrancelhas brancas
    draw.line([(6, 2), (10, 3)], fill=P['W'], width=1)
    draw.line([(13, 3), (17, 2)], fill=P['W'], width=1)

    # Olhos
    draw.rectangle([6, 4, 10, 8], fill=P['#'])
    draw.rectangle([7, 5, 9, 7], fill=P['Y'])
    draw.point((8, 6), fill=P['K'])
    draw.point((7, 5), fill=P['W'])

    draw.rectangle([13, 4, 17, 8], fill=P['#'])
    draw.rectangle([14, 5, 16, 7], fill=P['Y'])
    draw.point((15, 6), fill=P['K'])
    draw.point((14, 5), fill=P['W'])

    # Bico
    draw.polygon([(11, 6), (12, 6), (12, 9), (11, 9)], fill=P['A'])
    draw.point((12, 8), fill=P['a'])

    # Peito
    draw.ellipse([7, 9, 16, 15], fill=P['C'])
    draw.point((9, 11), fill=P['B'])
    draw.point((14, 11), fill=P['B'])

    # 2. Mapinha na base
    draw.polygon([(2, 14), (8, 13), (8, 21), (2, 22)], fill=P['M_SHADOW'])
    draw.polygon([(8, 13), (15, 13), (15, 21), (8, 21)], fill=P['M_PAPER'])
    draw.polygon([(15, 13), (21, 14), (21, 22), (15, 21)], fill=P['M_SHADOW'])

    draw.polygon([(2, 14), (8, 13), (15, 13), (21, 14), (21, 22), (15, 21), (8, 21), (2, 22)], outline=P['M_BORDER'])
    draw.line([(8, 13), (8, 21)], fill=P['M_GRID'], width=1)
    draw.line([(15, 13), (15, 21)], fill=P['M_GRID'], width=1)

    draw.line([(3, 16), (6, 18)], fill=P['M_WATER'], width=1)
    draw.line([(9, 15), (14, 15)], fill=P['M_GRID'], width=1)
    draw.line([(9, 19), (14, 19)], fill=P['M_GRID'], width=1)
    draw.line([(4, 20), (8, 17), (15, 17), (20, 18)], fill=P['M_ROAD'], width=1)

    draw.ellipse([10, 15, 12, 17], fill=P['M_PIN'])
    draw.point((11, 16), fill=P['W'])

    draw.point((7, 12), fill=P['A'])
    draw.point((8, 12), fill=P['A'])
    draw.point((15, 12), fill=P['A'])
    draw.point((16, 12), fill=P['A'])

    return img

im48 = generate_buraqueira_urb_48()
im24 = generate_buraqueira_urb_24()

p48 = os.path.join(output_dir, "Buraqueira_Urb_Icon_48.png")
p24 = os.path.join(output_dir, "Buraqueira_Urb_Icon_24.png")

im48.save(p48)
im24.save(p24)

# Gerar Base64 para embutir no C#
buf48 = BytesIO()
im48.save(buf48, format="PNG")
b64_48 = base64.b64encode(buf48.getvalue()).decode("ascii")

buf24 = BytesIO()
im24.save(buf24, format="PNG")
b64_24 = base64.b64encode(buf24.getvalue()).decode("ascii")

with open(os.path.join(output_dir, "icon_b64.txt"), "w") as f:
    f.write(f"// 48x48 Base64 Icon:\n{b64_48}\n\n// 24x24 Base64 Icon:\n{b64_24}\n")

print("Ícones gerados com sucesso em:", output_dir)
print("Tamanho Base64 48x48:", len(b64_48))

